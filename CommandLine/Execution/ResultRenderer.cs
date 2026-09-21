using CommandLine.Modules;
using CommandLineReimagined.Core;
using UIComponents.Components;

namespace Terminal.Execution;

/// <summary>
/// Renders a session's response into the console scene.
/// </summary>
/// <remarks>
/// Text for this phase. It used to turn directory entries into buttons carrying a
/// path, a context menu and a double-click action; those were built on path values,
/// and the filesystem is attribute records now. Decision 0012 keeps the desktop shell
/// out of scope, so the requirement is that it compiles and runs commands, and the
/// interactive listing comes back in Phase 7 if the owner wants it.
///
/// It takes the whole response rather than a value, because a fault is a value here
/// too: there is no exception to catch and no separate error path to render.
/// </remarks>
public sealed class ResultRenderer
{
    public void Render(Response response, CliBlock block)
    {
        foreach (var line in response.Output)
        {
            block.NewLineComponent().LinkNewTextBlock("output", line);
        }

        if (response.Fault is { } fault)
        {
            // The kind before the message, the way the browser page shows it.
            RenderError($"{fault.Value.Kind.ToString().ToLowerInvariant()}  {fault.Value.Message}", block);
            return;
        }

        if (response.Result is { } result && !result.Value.IsEmpty)
        {
            string text = ValueModule.display(result.Value);

            if (text.Length > 0)
            {
                block.NewLineComponent().LinkNewTextBlock("result", text);
            }
        }
    }

    public void RenderError(string message, CliBlock block) =>
        block.NewLineComponent().LinkNewTextBlock("error", message);
}
