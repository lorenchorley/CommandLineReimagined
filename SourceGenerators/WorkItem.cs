using Microsoft.CodeAnalysis;
using System;

namespace SourceGenerators;

class WorkItem
{
    public WorkItem(INamedTypeSymbol testClass)
    {
        TestClass = testClass ?? throw new ArgumentNullException(nameof(testClass));
    }

    public INamedTypeSymbol TestClass { get; }
}