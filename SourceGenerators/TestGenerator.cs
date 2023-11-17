using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SourceGenerators;

[Generator]
public class TestGenerator : ISourceGenerator
{
    private string GenerateTemplate(List<IPropertySymbol> properties, string componentTypeName, string ns)
    {
        return $$"""
            using System.Text;
            using EntityComponentSystem;
            using EntityComponentSystem.EventSourcing;

            namespace {{ns}};

            public class {{componentTypeName}}Creation : IComponentCreation
            {
                public EntityAccessor Entity { get; set; }

                public void ApplyTo(IdentifiableList list)
                {
                    Entity e = list.Get(Entity);

                    e.AddComponent(typeof({{componentTypeName}}));
                }

                public void Serialise(System.Text.StringBuilder sb, IdentifiableList list)
                {
                    throw new NotImplementedException();
                }
            }

            public class {{componentTypeName}}Differential : IComponentDifferential
            {
                public ComponentAccessor Component { get; set; }

                {{IndentLines(GeneratePublicModifiedFlagFields(properties), 1)}}
                {{IndentLines(GeneratePublicFields(properties), 1)}}

                public void ApplyTo(IdentifiableList list)
                {
                    {{componentTypeName}} component = ({{componentTypeName}})list.Get(Component);

                    {{IndentLines(GenerateDifferentialApplicators(properties), 2)}}
                }

                public void Serialise(System.Text.StringBuilder sb, IdentifiableList list)
                {
                    throw new NotImplementedException();
                }
            }

            public class {{componentTypeName}}Suppression : IComponentSuppression
            {
                public ComponentAccessor Component { get; set; }

                public void ApplyTo(IdentifiableList list)
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
                        
            public class {{componentTypeName}}Proxy : {{componentTypeName}}, IComponentProxy
            {
                public Action<IEvent> RegisterDifferential { get; init; }

                {{IndentLines(GeneratePrivateFields(properties), 1)}}
                {{IndentLines(GeneratePublicDifferentialProperties(componentTypeName, properties), 1)}}
            }
            
            """;
    }

    private string GeneratePrivateFields(List<IPropertySymbol> properties)
        => string.Join(Environment.NewLine, properties.Select(p => $"private {p.OriginalDefinition.Type.ToDisplayString()} _{ToCamelCase(p.OriginalDefinition.Name)};"));

    private string GeneratePublicFields(List<IPropertySymbol> properties)
        => string.Join(Environment.NewLine, properties.Select(p => $"public {p.OriginalDefinition.Type.ToDisplayString()} {p.OriginalDefinition.Name};"));
    
    private string GeneratePublicModifiedFlagFields(List<IPropertySymbol> properties)
        => string.Join(Environment.NewLine, properties.Select(p => $"public bool {p.OriginalDefinition.Name}_ModifiedFlag = false;"));

    private static string ToCamelCase(string propertyName)
    {
        return char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
    }

    private string GeneratePublicDifferentialProperties(string componentTypeName, List<IPropertySymbol> properties)
        => properties.Select(p => GeneratePublicDifferentialProperties(componentTypeName, p.OriginalDefinition.Name, p.OriginalDefinition.Type.ToDisplayString()))
                     .Join(Environment.NewLine + Environment.NewLine);

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
                    RegisterDifferential(new {{componentTypeName}}Differential()
                    {
                        {{propertyName}} = value,
                        {{propertyName}}_ModifiedFlag = true,
                        Component = new ComponentAccessor(this)
                    });
                }
            }
            """;
 
    private string GenerateDifferentialApplicators(List<IPropertySymbol> properties)
        => properties.Select(p => GenerateDifferentialApplicator(p.OriginalDefinition.Name))
                     .Join(Environment.NewLine + Environment.NewLine);

    private string GenerateDifferentialApplicator(string propertyName)
        => $$"""
            if ({{propertyName}}_ModifiedFlag)
            {
                component.{{propertyName}} = {{propertyName}};
            }
            """;


    public void Execute(GeneratorExecutionContext context)
    {
        if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
            return;

        foreach (var workItem in receiver.WorkItems)
        {
            var fileName = workItem.TestClass.FullName() + ".cs";

            if (workItem.TestClass.IsAbstract)
            {
                continue;
            }

            List<IPropertySymbol> properties = 
                workItem.TestClass
                        .GetMembers()
                        .OfType<IPropertySymbol>()
                        .Where(IsStateProperty)
                        .ToList();

            foreach (var property in properties)
            {
                if (!property.Type.IsVirtual)
                {
                    receiver.Log.Add($"Property {property.Name} is not virtual !");
                }
            }

            var generatedCode =
                GenerateTemplate(
                    properties,
                    workItem.TestClass.Name,
                    workItem.TestClass.FullNamespace());

            context.AddSource(fileName, SourceText.From(generatedCode, Encoding.UTF8));
        }

        context.AddSource("Logs", SourceText.From($@"/*{Environment.NewLine + string.Join(Environment.NewLine, receiver.Log) + Environment.NewLine}*/", Encoding.UTF8));
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
        return text.Replace(Environment.NewLine, Environment.NewLine + indent);
    }

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
    }

}