namespace Terminal.Execution;

/// <summary>
/// Where a command writes output while it runs.
/// </summary>
/// <remarks>
/// Commands return a <see cref="RuntimeValue"/> for their result, and the shell renders
/// that. This interface is for output that has to appear <em>during</em> execution, such
/// as a download's progress bar, which cannot wait for a return value.
///
/// It is an interface so execution does not depend on the ECS or on GDI+: CliBlock
/// implements it for the real console, and tests substitute a recorder. That is what
/// makes the execution layer testable headlessly.
/// </remarks>
public interface ICommandOutput
{
    IOutputLine NewLine();

    /// <summary>Discards a line that turned out to have nothing to say.</summary>
    void AbandonLine(IOutputLine line);
}

public interface IOutputLine
{
    IOutputText Write(string description, string text);
}

/// <summary>A written run of text that can still be changed, for progress indicators.</summary>
public interface IOutputText
{
    string Text { get; set; }
}
