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
        var privateBackingFields = IndentLines(GeneratePrivateDifferentialFields(properties), 1);
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

                {{privateBackingFields}}

                public void ApplyTo(IdentifiableList list)
                {
                    {{componentTypeName}} component = ({{componentTypeName}})list.Get(Component);

                    {{IndentLines(GenerateDifferentialApplicators(), 2)}}
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
                    sb.Append('\n');
                }
            }


                        
            public class {{componentTypeName}}Proxy : {{componentTypeName}}, IComponentProxy
            {
                public Action<IEvent> RegisterDifferential { get; init; }

                {{privateBackingFields}}
                {{IndentLines(GeneratePublicDifferentialProperties(componentTypeName, properties), 1)}}
            }
            
            """;
    }

    private string GeneratePrivateDifferentialFields(List<IPropertySymbol> properties)
        => string.Join("\n", properties.Select(FormatPropertyDefinition));

    private static string FormatPropertyDefinition(IPropertySymbol p)
    {
        string propertyName = p.OriginalDefinition.Name;
        string ns = p.OriginalDefinition.Type.FullNamespace();
        string fullyQualifiedName = p.OriginalDefinition.Type.ToDisplayString();

        string typeName = p.OriginalDefinition.Type.Name.ToString();
        return $"private {fullyQualifiedName} {ToPrivateCamelCase(propertyName)};";
    }

    private static string ToPrivateCamelCase(string propertyName)
    {
        return "_" + char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1);
    }

    private string GeneratePublicDifferentialProperties(string componentTypeName, List<IPropertySymbol> properties)
        => properties.Select(p => GeneratePublicDifferentialProperties(componentTypeName, p.propertyName, p.propertyType))
                     .Join("\n\n");

    private string GeneratePublicDifferentialProperties(string componentTypeName, string propertyName, string propertyType)
        => $$"""
            public override {{propertyType}} {{propertyName}}
            {
                get
                {
                    return {{ToPrivateCamelCase(propertyName)}};
                }
                set
                {
                    {{ToPrivateCamelCase(propertyName)}} = value;
                    RegisterDifferential(new {{componentTypeName}}Differential()
                    {
                        {{propertyName}} = value,
                        Component = new ComponentAccessor(this)
                    });
                }
            }
            """;
            //$$"""
            //public override string Text
            //{
            //    get
            //    {
            //        return _text;
            //    }
            //    set
            //    {
            //        _text = value;
            //        RegisterDifferential(new TextComponentDifferential()
            //        {
            //            Text = value,
            //            Component = new ComponentAccessor(this)
            //        });
            //    }
            //}
            //public override bool Highlighted
            //{
            //    get
            //    {
            //        return _highlighted;
            //    }
            //    set
            //    {
            //        _highlighted = value;
            //        RegisterDifferential(new TextComponentDifferential()
            //        {
            //            Highlighted = value,
            //            Component = new ComponentAccessor(this)
            //        });
            //    }
            //}
            //""";
    private string GenerateDifferentialApplicators()
        => $$"""
            if (Text is not null)
            {
                component.Text = Text;
            }
            if (Highlighted is not null)
            {
                component.Highlighted = Highlighted.Value;
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
                List<IPropertySymbol> properties = workItem.TestClass.GetMembers().OfType<IPropertySymbol>().ToList();

                var generatedCode =
                    GenerateTemplate(
                        properties,
                        workItem.TestClass.Name,
                        workItem.TestClass.FullNamespace());

                context.AddSource(fileName, SourceText.From(generatedCode, Encoding.UTF8));
            }

            context.AddSource("Logs", SourceText.From($@"/*{Environment.NewLine + string.Join(Environment.NewLine, receiver.Log) + Environment.NewLine}*/", Encoding.UTF8));
        }

        private string IndentLines(string text, int indentLevel)
        {
            string indent = new string('\t', indentLevel);
            return string.Join("\n" + indent, text.Split('\n'));
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

    }