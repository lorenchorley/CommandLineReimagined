using Microsoft.CodeAnalysis;
using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Collections.Generic;

namespace SourceGenerators
{
    /// <summary>
    /// This is used to process the syntax tree. The output is "work items", which are fed into the code generators.
    /// </summary>
    /// <remarks>
    /// Created on demand before each generation pass
    /// </remarks>
    class SyntaxReceiver : ISyntaxContextReceiver
    {
        public List<string> Log { get; } = new();
        public List<WorkItem> WorkItems { get; } = new();

        /// <summary>
        /// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation
        /// </summary>
        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            try
            {
                // any field with at least one attribute is a candidate for property generation
                if (context.Node is ClassDeclarationSyntax classDeclarationSyntax)
                {
                    var testClass = (INamedTypeSymbol)context.SemanticModel.GetDeclaredSymbol(context.Node)!;

                    if (testClass?.BaseType?.Name == "Component")
                    {
                        Log.Add($"Found a class named {testClass.Name}");
                        WorkItems.Add(new WorkItem(testClass));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Add("Error parsing syntax: " + ex.ToString());
            }
        }


    }
}