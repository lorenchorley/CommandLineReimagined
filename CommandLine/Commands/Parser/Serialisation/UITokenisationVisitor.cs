using CommandLine.Modules;
using UIComponents;
using UIComponents.Components;
using EntityComponentSystem;
using Isagri.Reporting.Quid.RequestFilters.SemanticTree;

namespace Terminal.Commands.Parser.Serialisation;

public class UITokenisationVisitor : VisitorBase
{
    private readonly List<LineSegmentComponent> _segments = new();

    private Entity _panelEntity;
    private LineComponent CurrentLine;

    public UITokenisationVisitor(Entity panelEntity)
    {
        _panelEntity = panelEntity;
        CurrentLine = 
            panelEntity.Children
                       .ToArray()
                       .Choose(s => s.TryGetComponent<LineComponent>())
                       .LastOrDefault()
                       ?? _panelEntity.NewChildEntity("Input prompt").AddComponent<LineComponent>();
    }

    public override void Append(string str)
    {
        var segment = CurrentLine.LinkNewTextBlock("Text", str);

        _segments.Add(segment);
    }

    public override void Append(char c)
    {
        Append(c.ToString());
    }

    public override void AppendNewLine()
    {
        Entity entity = _panelEntity.NewChildEntity("Input prompt");
        CurrentLine = entity.AddComponent<LineComponent>();
    }

    public override string GetResult()
    {
        return _segments.ToString();
    }
}
