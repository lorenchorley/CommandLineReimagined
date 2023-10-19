using CommandLineReimagine.Console;
using CommandLineReimagine.Console.Components;

public static class ECSExtensions
{
    public static void AddTextBlock(this Line line, string description, string text)
    {
        var segment = line.ECS.NewEntity(description).AddComponent<TextBlock>();
        segment.Text = text;
        line.AddLineSegment(segment);
    }
    public static Button AddButton(this Line line, string description, string text)
    {
        var entity = line.ECS.NewEntity(description);
        Button button = entity.AddComponent<Button>();
        button.Text = text;
        line.AddLineSegment(button);
        return button;
    }
}
