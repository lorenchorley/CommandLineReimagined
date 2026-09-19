using Commands;
using Commands.Parser.SemanticTree;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Scoping;
using Terminal.Variables;

namespace Terminal.Execution;

/// <summary>
/// Executes a parsed command tree.
/// </summary>
/// <remarks>
/// Previously the shell ran <c>OrderedCommands.First()</c> and discarded the rest, so a
/// pipe parsed but only its first command ever ran, and only the CLI form executed at
/// all. This walks the whole list, threading each command's <see cref="RuntimeValue"/>
/// into the next, and handles all three expression forms the grammar produces.
/// </remarks>
public sealed class CommandEvaluator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ArgumentBinder _binder;
    private readonly IReadOnlyList<CommandDefinition> _definitions;
    private readonly CommandHistory _history;

    public CommandEvaluator(
        IServiceProvider serviceProvider,
        IEnumerable<CommandDefinition> definitions,
        CommandHistory history,
        ArgumentBinder? binder = null)
    {
        _serviceProvider = serviceProvider;
        _definitions = definitions.ToList();
        _history = history;
        _binder = binder ?? new ArgumentBinder();
    }

    public async Task<RuntimeValue> ExecuteAsync(
        RootNode tree,
        ICommandOutput output,
        Scope scope,
        CancellationToken cancellation = default)
    {
        switch (tree)
        {
            case EmptyCommand:
                return RuntimeValue.Empty;

            case PipedCommandList pipeline:
            {
                if (pipeline.OrderedCommands.Count == 0)
                {
                    return RuntimeValue.Empty;
                }

                // Each command's result becomes the next one's input.
                RuntimeValue current = RuntimeValue.Empty;
                foreach (var expression in pipeline.OrderedCommands)
                {
                    cancellation.ThrowIfCancellationRequested();
                    current = await ExecuteExpressionAsync(expression, current, output, scope, cancellation);
                }

                return current;
            }

            default:
                throw new ConsoleError($"Cannot execute a {tree.GetType().Name}.");
        }
    }

    private Task<RuntimeValue> ExecuteExpressionAsync(
        CommandExpression expression,
        RuntimeValue input,
        ICommandOutput output,
        Scope scope,
        CancellationToken cancellation) =>
        expression.Expression.Match(
            // cmd(a, b: c)
            function => ExecuteCommandAsync(
                function.Id.Name, function.Arguments, input, output, scope, cancellation),

            // cmd a b
            cli => ExecuteCommandAsync(
                cli.Name.Name, cli.Arguments, input, output, scope, cancellation),

            // <thing a=1/>
            instance => Task.FromResult(EvaluateInstance(instance, scope)));

    private async Task<RuntimeValue> ExecuteCommandAsync(
        string name,
        CommandArguments? arguments,
        RuntimeValue input,
        ICommandOutput output,
        Scope scope,
        CancellationToken cancellation)
    {
        var definition = Find(name);
        CommandParameterValue[] bound;

        if (definition is null)
        {
            // Preserved from the original shell: an unrecognised name is reported by a
            // command rather than by throwing, so it renders like any other output.
            definition = Find("UnknownCommand")
                ?? throw new ConsoleError($"Unknown command : {name}");

            bound = new[]
            {
                new CommandParameterValue
                {
                    Parameter = definition.Parameters.FirstOrDefault(),
                    Value = new TextValue(name),
                },
            };
        }
        else
        {
            bound = _binder.Bind(arguments, definition, input, scope);
        }

        var action = (ICommandAction)_serviceProvider.GetRequiredService(definition.CommandActionType);
        var invocation = new CommandInvocation(definition, bound, input, output, scope, cancellation);

        switch (action)
        {
            case CommandActionSync sync:
            {
                _history.Register(action, invocation);
                return sync.Invoke(invocation);
            }

            case CommandActionAsync async:
            {
                _history.Register(action, invocation);

                // Awaited rather than fired and forgotten, so a pipe stage actually
                // finishes before the next one reads its value. The shell keeps the UI
                // responsive by not awaiting the whole pipeline on the input thread.
                async.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                var task = async.BeginInvoke(invocation);
                async.CurrentTask = task;

                try
                {
                    var result = await task;
                    await async.EndInvoke(invocation);
                    return result;
                }
                catch (Exception) when (task.IsCanceled || task.IsFaulted)
                {
                    await async.FailedInvoke(invocation, task);
                    throw;
                }
                finally
                {
                    async.CancellationTokenSource = null;
                    async.CurrentTask = null;
                }
            }

            default:
                throw new ConsoleError($"Unknown command type : {action.GetType().Name}");
        }
    }

    /// <summary>
    /// Builds an object value from an instance tag, binding it to a variable when the tag
    /// names one: <c>&lt;size|dimension value=3/&gt;</c> leaves <c>$size</c> in scope.
    /// </summary>
    private RuntimeValue EvaluateInstance(InstanceTag tag, Scope scope)
    {
        switch (tag)
        {
            case ObjectInstance instance:
            {
                var value = BuildObject(instance, scope);
                Bind(instance.VariableName, value, scope);
                return value;
            }

            case ComponentInstance component:
            {
                var value = BuildComponent(component, scope);
                Bind(component.VariableName, value, scope);
                return value;
            }

            // <$name> reads a variable back.
            case VariableTag reference:
            {
                var variable = scope.GetVariable(reference.Name.Name)
                    ?? throw new ConsoleError($"Unknown variable : ${reference.Name.Name}");

                return variable.Value;
            }

            default:
                throw new ConsoleError($"Cannot evaluate a {tag.GetType().Name}.");
        }
    }

    private static void Bind(VariableName? name, RuntimeValue value, Scope scope)
    {
        if (name is not null)
        {
            scope.SetVariable(new Variable(name.Name, value));
        }
    }

    private ComponentValue BuildComponent(ComponentInstance instance, Scope scope)
    {
        var attributes = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var attribute in instance.Attributes?.Attributes ?? new List<TagAttribute>())
        {
            attributes[attribute.Name.Name] = _binder.Evaluate(attribute.Value, scope);
        }

        var children = new List<RuntimeValue>();

        foreach (var child in instance.Children?.Tags ?? new List<Tag>())
        {
            children.Add(child is InstanceTag nested
                ? EvaluateInstance(nested, scope)
                : throw new ConsoleError($"Cannot evaluate a child {child.GetType().Name}."));
        }

        return new ComponentValue(instance.ComponentType.Value, attributes, children);
    }

    private ObjectValue BuildObject(ObjectInstance instance, Scope scope)
    {
        var attributes = new Dictionary<string, RuntimeValue>(StringComparer.OrdinalIgnoreCase);

        foreach (var attribute in instance.Attributes?.Attributes ?? new List<TagAttribute>())
        {
            attributes[attribute.Name.Name] = _binder.Evaluate(attribute.Value, scope);
        }

        var children = new List<ObjectValue>();

        foreach (var child in instance.Children?.Tags ?? new List<Tag>())
        {
            if (child is ObjectInstance childInstance)
            {
                children.Add(BuildObject(childInstance, scope));
            }
            else if (child is ComponentInstance componentChild)
            {
                // An entity's children can be components as well as other entities.
                BuildComponent(componentChild, scope);
            }
            else
            {
                throw new ConsoleError($"Cannot evaluate a child {child.GetType().Name}.");
            }
        }

        return new ObjectValue(instance.ObjectType.Value, attributes, children);
    }

    private CommandDefinition? Find(string name) =>
        _definitions.FirstOrDefault(
            d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
}
