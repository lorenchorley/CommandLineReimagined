using Console;
using Console.Components;

public static class LineExtensions
{
    public static TextComponent LinkNewTextBlock(this LineComponent line, string description, string text, bool highlighted = false)
    {
        TextComponent textComponent = line.ECS.NewEntity(description).AddComponent<TextComponent>();
        textComponent.Text = text;
        textComponent.Highlighted = highlighted;
        line.AddLineSegment(textComponent);

        return textComponent;
    }

    public static HighlightComponent LinkNewTextHighlight(this LineComponent line, TextComponent textComponent, int lineNumber, int columnNumber)
    {
        HighlightComponent highlight = textComponent.ECS.NewEntity("").AddComponent<HighlightComponent>();
        highlight.TextComponent = textComponent;
        highlight.Line = lineNumber;
        highlight.Column = columnNumber;

        line.AddLineSegment(highlight);

        return highlight;
    }

    public static ButtonComponent LinkNewButton(this LineComponent line, string description, string text)
    {
        var entity = line.ECS.NewEntity(description);
        ButtonComponent button = entity.AddComponent<ButtonComponent>();
        button.Text = text;
        line.AddLineSegment(button);

        return button;
    }

    //public static CursorComponent LinkNewCursor(this LineComponent line, TextComponent textComponent, int position)
    //{
    //    CursorComponent cursor = textComponent.ECS.NewEntity("").AddComponent<CursorComponent>();
    //    cursor.TextComponent = textComponent;
    //    cursor.Position = position;

    //    line.AddLineSegment(cursor);

    //    return cursor;
    //}

}
