using System.Windows;
using Terminal.Execution;

namespace Application;

/// <summary>
/// Shuts the desktop shell down when the <c>exit</c> command asks.
/// </summary>
/// <remarks>
/// The command itself no longer references WPF; it depends on
/// <see cref="IApplicationLifetime"/>, and this is the desktop implementation. The web
/// host and tests register their own, which is why <c>exit</c> compiles in a project
/// that has no window.
/// </remarks>
public sealed class WpfApplicationLifetime : IApplicationLifetime
{
    public void Shutdown() =>
        System.Windows.Application.Current?.Dispatcher.Invoke(
            () => System.Windows.Application.Current.Shutdown());
}
