using Application.FrameworkAccessors;
using EntityComponentSystem;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Application;

/// <summary>
/// La fenêtre principale qui héberge le canvas et d'autres composants qui permet l'intéraction
/// </summary>
public partial class MainWindow : Window, IECSSubsystem
{
    private readonly CanvasAccessor _canvasAccessor;
    private readonly InputAccessor _inputTextBoxAccessor;
    private readonly ContextMenuAccessor _contextMenuAccessor;

    public MainWindow(
        CanvasAccessor canvasAccessor,
        InputAccessor inputTextBoxAccessor,
        ContextMenuAccessor contextMenuAccessor
        )
    {
        InitializeComponent();
        _canvasAccessor = canvasAccessor;
        _inputTextBoxAccessor = inputTextBoxAccessor;
        _contextMenuAccessor = contextMenuAccessor;
    }

    public void OnInit()
    {
        _canvasAccessor.SetFrameworkElement(Canvas, CanvasImage);
        _inputTextBoxAccessor.SetFrameworkElement(Input);

        foreach ((string Key, ContextMenu menu) in GetContextMenus())
        {
            _contextMenuAccessor.AddFrameworkElement(Key, menu);
        }
    }

    public void OnStart()
    {
        Show();
    }

    private IEnumerable<(string Key, ContextMenu menu)> GetContextMenus()
    {
        foreach (string key in Resources.Keys)
        {
            var resource = FindResource(key);

            if (resource is not ContextMenu contextMenu)
            {
                continue;
            }

            yield return (key, contextMenu);
        }
    }
}
