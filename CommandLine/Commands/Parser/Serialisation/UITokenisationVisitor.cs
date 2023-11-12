using CommandLine.Modules;
using Console;
using Console.Components;
using EntityComponentSystem;
using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Terminal.Commands.Parser.Serialisation;

public class UITokenisationVisitor : VisitorBase
{
    private readonly List<LineSegmentComponent> _segments = new();

    private List<LineComponent> Lines;
    private LineComponent CurrentLine;

    public UITokenisationVisitor(List<LineComponent> lines)
    {
        Lines = lines;
        CurrentLine = Lines.Last();
    }

    public override void Append(string str)
    {
        var segment = CurrentLine.LinkNewTextBlock("", str);

        _segments.Add(segment);
    }

    public override void Append(char c)
    {
        Append(c.ToString());
    }

    public override void AppendNewLine()
    {
        Entity entity = CurrentLine.ECS.NewEntity("Input prompt and command");
        CurrentLine = entity.AddComponent<LineComponent>();
        Lines.Add(CurrentLine);
    }

    public override string GetResult()
    {
        return _segments.ToString();
    }
}
