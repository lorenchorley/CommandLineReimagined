using System.Text;
using System;

namespace SourceGenerators
{
    class CodeWriter
    {
        public CodeWriter()
        {
            m_ScopeTracker = new(this); //We only need one. It can be reused.
        }
        StringBuilder Content { get; } = new();
        int IndentLevel { get; set; }
        ScopeTracker m_ScopeTracker { get; } //We only need one. It can be reused.
        public void Append(string line) => Content.Append(line);

        public void AppendLine(string line) => Content.Append(new string('\t', IndentLevel)).AppendLine(line);
        public void AppendLine() => Content.AppendLine();
        public IDisposable BeginScope(string line)
        {
            AppendLine(line);
            return BeginScope();
        }
        public IDisposable BeginScope()
        {
            Content.Append(new string('\t', IndentLevel)).AppendLine("{");
            IndentLevel += 1;
            return m_ScopeTracker;
        }

        public void EndLine() => Content.AppendLine();

        public void EndScope()
        {
            IndentLevel -= 1;
            Content.Append(new string('\t', IndentLevel)).AppendLine("}");
        }

        public void StartLine() => Content.Append(new string('\t', IndentLevel));
        public override string ToString() => Content.ToString();

        public IDisposable StartTest(string testName)
        {
            AppendLine("[TestMethod]");
            return BeginScope($"public void @{testName}()");
        }
        public void AssertAreEqual(string expected, string actual, string message)
        {
            AppendLine($@"Assert.AreEqual({expected}, {actual}, ""{EscapeString(message)}"");");
        }

        string EscapeString(string text) => text.Replace("\"", "\"\"");

        class ScopeTracker : IDisposable
        {
            public ScopeTracker(CodeWriter parent)
            {
                Parent = parent;
            }
            public CodeWriter Parent { get; }

            public void Dispose()
            {
                Parent.EndScope();
            }
        }
    }
}