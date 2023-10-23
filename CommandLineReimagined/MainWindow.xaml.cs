using CommandLine.Modules;
using Commands;
using Console;
using Console.Components;
using Rendering;
using EntityComponentSystem;
using EntityComponentSystem.RayCasting;
using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using InteractionLogic;
using System.Windows.Threading;

namespace CommandLineReimagined;

/// <summary>
/// La fenêtre principale qui héberge le canvas et d'autres composants qui permet l'intéraction
/// </summary>
public partial class MainWindow : Window
{
    private const int ACTIVE_FRAME_RATE = 30;

    public MainWindow(
        CanvasAccessor canvasAccessor,
        InputAccessor inputTextBoxAccessor,
        RenderLoop renderLoop
        )
    {
        InitializeComponent();

        canvasAccessor.SetFrameworkElement(Canvas, CanvasImage);
        inputTextBoxAccessor.SetFrameworkElement(Input);

        Dispatcher.BeginInvoke((Action)(() => {
            //renderLoop.SetDrawAction(_consoleRenderer.Draw);
            //renderLoop.SetRenderToScreenAction(UpdateVisual);
        }), DispatcherPriority.ApplicationIdle);
    }

}
