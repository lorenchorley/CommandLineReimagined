using InteractionLogic.FrameworkAccessors;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace CommandLineReimagined;

/// <summary>
/// La fenêtre principale qui héberge le canvas et d'autres composants qui permet l'intéraction
/// </summary>
public partial class MainWindow : Window
{

    public MainWindow(
        CanvasAccessor canvasAccessor,
        InputAccessor inputTextBoxAccessor,
        ContextMenuAccessor contextMenuAccessor
        )
    {
        InitializeComponent();

        canvasAccessor.SetFrameworkElement(Canvas, CanvasImage);
        inputTextBoxAccessor.SetFrameworkElement(Input);

        foreach ((string Key, ContextMenu menu) in GetContextMenus())
        {
            contextMenuAccessor.AddFrameworkElement(Key, menu);
        }
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
