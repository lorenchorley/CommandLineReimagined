using Commands.Parser.SemanticTree;
using System.Text;

namespace Isagri.Reporting.Quid.RequestFilters.SemanticTree;

public class SerialisationVisitor : VisitorBase
{
    private readonly StringBuilder _sb = new StringBuilder();

    public override void Append(string str)
    {
        _sb.Append(str);
    }

    public override void Append(char c)
    {
        _sb.Append(c);
    }

    public override void AppendNewLine()
    {
        Append('\n');
    }

    public override string GetResult()
    {
        return _sb.ToString();
    }
}
