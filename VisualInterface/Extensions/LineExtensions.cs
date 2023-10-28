using Console;
using Console.Components;

public static class LineExtensions
{
    public static void AddTextBlock(this LineComponent line, string description, string text)
    {
        var segment = line.ECS.NewEntity(description).AddComponent<TextComponent>();
        segment.Text = text;
        line.AddLineSegment(segment);
    }
    public static ButtonComponent AddButton(this LineComponent line, string description, string text)
    {
        var entity = line.ECS.NewEntity(description);
        ButtonComponent button = entity.AddComponent<ButtonComponent>();
        button.Text = text;
        line.AddLineSegment(button);
        return button;
    }
}
