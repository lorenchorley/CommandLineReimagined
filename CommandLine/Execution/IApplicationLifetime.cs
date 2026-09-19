namespace Terminal.Execution;

/// <summary>
/// How a command asks the host to shut down.
/// </summary>
/// <remarks>
/// Keeps <c>exit</c> free of any reference to WPF, so the same command works under the
/// desktop shell, the web host and tests.
/// </remarks>
public interface IApplicationLifetime
{
    void Shutdown();
}

/// <summary>Used where there is nothing to shut down, such as tests.</summary>
public sealed class NoOpApplicationLifetime : IApplicationLifetime
{
    public bool ShutdownRequested { get; private set; }

    public void Shutdown() => ShutdownRequested = true;
}
