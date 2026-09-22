using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace SourceGenerators;

/// <summary>
/// Generates the creation and differential types that back every ECS component.
/// </summary>
/// <remarks>
/// An incremental generator. It was written against ISourceGenerator with an
/// ISyntaxContextReceiver, which Roslyn 5 rejects outright (RS1042): the receiver ran
/// over every syntax node on every keystroke with no caching. The work is the same, and
/// the generated code is unchanged; only the plumbing that feeds it differs.
/// </remarks>
[Generator]
public class ComponentGenerator : IIncrementalGenerator
{
    /// <summary>
    /// The line ending the generated code uses.
    /// </summary>
    /// <remarks>
    /// Fixed rather than Environment.NewLine, which analyzers are forbidden to read
    /// (RS1035) for good reason: it makes a generator's output depend on the machine
    /// that ran it. The emitted text is normalised to this ending before it is added.
    /// </remarks>
    private const string NewLine = "\n";

    private string GenerateTemplate(List<IPropertySymbol> properties, string componentTypeName, string ns)
    {
        return $$"""
            using System.Text;
            using EntityComponentSystem;
            using EntityComponentSystem.EventSourcing;
            using System.Diagnostics;
            
            namespace {{ns}};

            public class {{componentTypeName}}Creation : IComponentCreation
            {
                public bool AppliedToActive { get; set; } = false;
                public bool AppliedToShadow { get; set; } = false;
                public EntityIndex Entity { get; set; } // Parent entity
                public ComponentIndex Component { get; set; } // Component to create
                public Component CreatedComponent { get; set; } = null!;

                public {{componentTypeName}}Creation(EntityIndex entity) 
                {
                    Entity = entity;
                    ECS.ComponentCreationCheck(GetType().Name, Entity);
                }

                public void ApplyTo(IdentifiableList list, TreeType treeType)
                {
                    Entity e = list.Get(Entity);

                    CreatedComponent = e.InternalAddComponent(typeof({{componentTypeName}}Proxy), Component, treeType);
                    list.Set(CreatedComponent);
                }

                public void Serialise(System.Text.StringBuilder sb, IdentifiableList list)
                {
                    var component = list.Get(Component);

                    sb.Append(nameof({{componentTypeName}}Creation));
                    sb.Append(" (Entity : ");
                    sb.Append(component.Entity.Name);
                    sb.Append(')');
                    sb.Append(Environment.NewLine);
                }
            }

            public class {{componentTypeName}}Differential : IComponentDifferential
            {
                public ComponentIndex Component { get; set; }

                {{IndentLines(GeneratePublicModifiedFlagFields(properties), 1)}}
                {{IndentLines(GeneratePublicFields(properties), 1)}}

                public void ApplyTo(IdentifiableList list, TreeType treeType)
                {
                    {{componentTypeName}} component = ({{componentTypeName}})list.Get(Component);
                    var proxy = (IComponentProxy)component;
                    
                    proxy.DifferentialActive = false;

                    {{IndentLines(GenerateDifferentialApplicators(properties), 2)}}

                    if (treeType == TreeType.Active)
                        proxy.DifferentialActive = true;
                }

                public void Serialise(System.Text.StringBuilder sb, IdentifiableList list)
                {
                    var component = list.Get(Component);

                    sb.Append(nameof({{componentTypeName}}Differential));
                    sb.Append(" (Entity : ");
                    sb.Append(component.Entity.Name);
                    sb.Append(')');
                    sb.Append(Environment.NewLine);
                }
            }

            public class {{componentTypeName}}Suppression : IComponentSuppression
            {
                public ComponentIndex Component { get; set; }

                public void ApplyTo(IdentifiableList list, TreeType treeType)
                {
                    Component component = list.Get(Component);
                    component.Destroy();
                    list.Unset(Component);
                }

                public void Serialise(System.Text.StringBuilder sb, IdentifiableList list)
                {
                    var component = list.Get(Component);

                    sb.Append(nameof({{componentTypeName}}Suppression));
                    sb.Append(" (Entity : ");
                    sb.Append(component.Entity.Name);
                    sb.Append(')');
                    sb.Append(Environment.NewLine);
                }
            }
             
            [DebuggerDisplay("Component \"{{componentTypeName}}\" On {Entity.Name}")]
            public class {{componentTypeName}}Proxy : {{componentTypeName}}, IComponentProxy
            {
                public bool DifferentialActive { get; set; } = true;
                public Action<IEvent> RegisterDifferential { get; init; } = null!;

                //public IComponentCreation GenerateCreationEvent()
                //{
                //    return new {{componentTypeName}}Creation()
                //    {
                //        Entity = new EntityIndex(Entity),
                //        Component = new ComponentIndex(this)
                //    };
                //}

                public IComponentSuppression GenerateSuppressionEvent()
                {
                    return new {{componentTypeName}}Suppression()
                    {
                        Component = new ComponentIndex(this)
                    };
                }

                {{IndentLines(GeneratePrivateFields(properties), 1)}}
                {{IndentLines(GeneratePublicDifferentialProperties(componentTypeName, properties), 1)}}
            }
            
            """;
    }

    private string GeneratePrivateFields(List<IPropertySymbol> properties)
        => string.Join(NewLine, properties.Select(p => $"private {p.OriginalDefinition.Type.ToDisplayString()} _{ToCamelCase(p.OriginalDefinition.Name)} = default!;"));

    private string GeneratePublicFields(List<IPropertySymbol> properties)
        => string.Join(NewLine, properties.Select(p => $"public {p.OriginalDefinition.Type.ToDisplayString()} {p.OriginalDefinition.Name} = default!;"));
    
    private string GeneratePublicModifiedFlagFields(List<IPropertySymbol> properties)
        => string.Join(NewLine, properties.Select(p => $"public bool {p.OriginalDefinition.Name}_ModifiedFlag = false;"));

    private static string ToCamelCase(string propertyName)
    {
        return char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
    }

    private string GeneratePublicDifferentialProperties(string componentTypeName, List<IPropertySymbol> properties)
        => properties.Select(p => GeneratePublicDifferentialProperties(componentTypeName, p.OriginalDefinition.Name, p.OriginalDefinition.Type.ToDisplayString()))
                     .Join(NewLine + NewLine);

    private string GeneratePublicDifferentialProperties(string componentTypeName, string propertyName, string propertyType)
        => $$"""
            public override {{propertyType}} {{propertyName}}
            {
                get
                {
                    return _{{ToCamelCase(propertyName)}};
                }
                set
                {
                    _{{ToCamelCase(propertyName)}} = value;
                    if (DifferentialActive) 
                    {
                        RegisterDifferential(new {{componentTypeName}}Differential()
                        {
                            {{propertyName}} = value,
                            {{propertyName}}_ModifiedFlag = true,
                            Component = new ComponentIndex(this)
                        });
                    }
                }
            }
            """;
 
    private string GenerateDifferentialApplicators(List<IPropertySymbol> properties)
        => properties.Select(p => GenerateDifferentialApplicator(p.OriginalDefinition.Name))
                     .Join(NewLine + NewLine);

    private string GenerateDifferentialApplicator(string propertyName)
        => $$"""
            if ({{propertyName}}_ModifiedFlag)
            {
                component.{{propertyName}} = {{propertyName}};
            }
            """;


    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Every class declaration is examined, and the ones deriving from Component are
        // kept. Returning a candidate rather than a symbol keeps the log entries the old
        // receiver collected while it walked the tree.
        var candidates = context.SyntaxProvider
                                .CreateSyntaxProvider(
                                    predicate: static (node, _) => node is ClassDeclarationSyntax,
                                    transform: static (syntaxContext, _) => Examine(syntaxContext))
                                .Where(static candidate => candidate is not null)
                                .Select(static (candidate, _) => candidate!);

        context.RegisterSourceOutput(candidates.Collect(), (production, found) => Emit(production, found));
    }

    /// <summary>Decides whether one class declaration is a component, and notes what it saw.</summary>
    private static Candidate? Examine(GeneratorSyntaxContext context)
    {
        try
        {
            if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol declared)
            {
                return null;
            }

            return IsComponent(declared)
                ? new Candidate(declared, $"Found a class named {declared.Name}")
                : null;
        }
        catch (Exception exception)
        {
            return new Candidate(null, "Error parsing syntax: " + exception);
        }
    }

    private static bool IsComponent(INamedTypeSymbol? type)
    {
        INamedTypeSymbol? baseType = type?.BaseType;

        if (baseType is null)
        {
            return false;
        }

        return string.Equals(baseType.Name, "Component", StringComparison.Ordinal) || IsComponent(baseType);
    }

    private static void Emit(SourceProductionContext context, ImmutableArray<Candidate> candidates)
    {
        var log = new List<string>();
        var written = new HashSet<string>(StringComparer.Ordinal);
        var generator = new ComponentGenerator();

        foreach (var candidate in candidates)
        {
            log.Add(candidate.Log);

            if (candidate.Component is null || candidate.Component.IsAbstract)
            {
                continue;
            }

            string fileName = candidate.Component.FullName() + ".cs";

            // A partial class is declared more than once and would otherwise be
            // generated twice under the same name, which is a hard error.
            if (!written.Add(fileName))
            {
                continue;
            }

            List<IPropertySymbol> properties =
                candidate.Component
                         .GetMembers()
                         .OfType<IPropertySymbol>()
                         .Where(IsStateProperty)
                         .ToList();

            foreach (var property in properties)
            {
                if (!property.Type.IsVirtual)
                {
                    log.Add($"Property {property.Name} is not virtual !");
                }
            }

            string generatedCode = generator.GenerateTemplate(
                properties,
                candidate.Component.Name,
                candidate.Component.FullNamespace());

            context.AddSource(fileName, SourceText.From(Normalise(generatedCode), Encoding.UTF8));
        }

        context.AddSource(
            "Logs",
            SourceText.From(Normalise($@"/*{NewLine + string.Join(NewLine, log) + NewLine}*/"), Encoding.UTF8));
    }

    /// <summary>
    /// One line ending throughout, whatever endings the template literals in this file
    /// happen to have been saved with.
    /// </summary>
    private static string Normalise(string text) => text.Replace("\r\n", NewLine).Replace("\r", NewLine);

    /// <summary>A class that turned out to be a component, or a note about one that did not.</summary>
    private sealed class Candidate
    {
        public Candidate(INamedTypeSymbol? component, string log)
        {
            Component = component;
            Log = log;
        }

        public INamedTypeSymbol? Component { get; }

        public string Log { get; }
    }

    private static bool IsStateProperty(IPropertySymbol property)
    {
        var attributes = property.GetAttributes();
        foreach (var attribute in attributes)
        {
            if (string.Equals(attribute?.AttributeClass?.Name ?? "", "StateAttribute", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private string IndentLines(string text, int indentLevel)
    {
        string indent = new string('\t', indentLevel);
        return text.Replace(NewLine, NewLine + indent);
    }


}