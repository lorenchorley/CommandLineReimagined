using CommandLine.Modules;
using UIComponents.Components;

namespace Terminal.Execution;

/// <summary>
/// Renders a command's result into the console scene.
/// </summary>
/// <remarks>
/// This holds the interactive output that used to live inside <c>ls</c>: directory
/// entries become buttons carrying PathInformation, a context menu and a double-click
/// action. Moving it here means commands return data and the shell decides how it looks,
/// so results can also flow into a pipe or be asserted on in a test.
/// </remarks>
public sealed class ResultRenderer
{
    public void Render(RuntimeValue value, CliBlock block)
    {
        switch (value)
        {
            case EmptyValue:
                return;

            case ListValue list:
            {
                if (list.Items.Count == 0)
                {
                    return;
                }

                var line = block.NewLineComponent();
                foreach (var item in list.Items)
                {
                    RenderInline(item, line);
                }

                return;
            }

            default:
                RenderInline(value, block.NewLineComponent());
                return;
        }
    }

    private void RenderInline(RuntimeValue value, LineComponent line)
    {
        switch (value)
        {
            case PathValue path when path.Kind == PathKind.Parent:
                line.LinkNewButton("ls up", " up ")
                    .AddComponent<ContextMenuSource>(c => c.ContextMenuName = "PathNavigationContextMenu")
                    .AddComponent<DoubleClickAction>(c => c.ActionName = "Up");
                return;

            case PathValue path when path.Kind == PathKind.Directory:
                line.LinkNewButton("ls folder", $" {path.Name}\\ ")
                    .AddComponent<PathInformation>(c => c.Path = path.Path)
                    .AddComponent<ContextMenuSource>(c => c.ContextMenuName = "PathNavigationContextMenu")
                    .AddComponent<DoubleClickAction>(c => c.ActionName = "Enter");
                return;

            case PathValue path:
                line.LinkNewButton("ls file", $" {path.Name} ")
                    .AddComponent<PathInformation>(c => c.Path = path.Path)
                    .AddComponent<ContextMenuSource>(c => c.ContextMenuName = "FileNavigationContextMenu")
                    .AddComponent<DoubleClickAction>(c => c.ActionName = "ShowContents");
                return;

            case ListValue nested:
                foreach (var item in nested.Items)
                {
                    RenderInline(item, line);
                }

                return;

            default:
                line.LinkNewTextBlock("result", value.ToDisplayString());
                return;
        }
    }

    public void RenderError(string message, CliBlock block) =>
        block.NewLineComponent().LinkNewTextBlock("error", message, highlighted: true);
}
