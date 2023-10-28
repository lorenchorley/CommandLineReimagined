using CommandLine.Modules;
using Console;
using EntityComponentSystem;
using EntityComponentSystem.RayCasting;
using InteractionLogic.FrameworkAccessors;
using Rendering;
using System.Windows;
using System.Windows.Controls;

namespace InteractionLogic;

public class CanvasRenderingEventHandler
{
    private readonly CanvasAccessor _canvasAccessor;
    private readonly InputAccessor _inputAccessor;
    private readonly RenderLoop _renderLoop;
    private readonly ConsoleLayout _consoleLayout;
    private readonly ECS _ecs;
    private readonly PathModule _pathModule;

    public CanvasRenderingEventHandler(CanvasAccessor canvasAccessor, InputAccessor inputAccessor, RenderLoop renderLoop, ConsoleLayout consoleLayout, ECS ecs, PathModule pathModule)
    {
        _canvasAccessor = canvasAccessor;
        _inputAccessor = inputAccessor;
        _renderLoop = renderLoop;
        _consoleLayout = consoleLayout;
        _ecs = ecs;
        _pathModule = pathModule;

        _canvasAccessor.RegisterEventHandlers<CanvasRenderingEventHandler>(RegisterEventHandlers, UnregisterEventHandlers);
    }

    private void RegisterEventHandlers(Canvas canvas, Image image)
    {
        if (canvas.IsLoaded)
        {
            Canvas_Loaded(null, new RoutedEventArgs());
        }

        canvas.Loaded += Canvas_Loaded;
        canvas.SizeChanged += Canvas_SizeChanged;
    }

    private void UnregisterEventHandlers(Canvas canvas, Image image)
    {
        canvas.Loaded -= Canvas_Loaded;
        canvas.SizeChanged -= Canvas_SizeChanged;
    }

    public void Canvas_Loaded(object sender, RoutedEventArgs e)
    {
        //_renderLoop = new(ACTIVE_FRAME_RATE, (int)Canvas.ActualWidth, (int)Canvas.ActualHeight, _consoleRenderer.Draw, UpdateVisual);
        _renderLoop.SetCanvasSize((int)_canvasAccessor.Canvas!.ActualWidth, (int)_canvasAccessor.Canvas!.ActualHeight);

        RefreshVisualInputPrompt();
        _renderLoop.RefreshOnce();
    }

    public void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        //_buffer.SetSize(e.NewSize.Width, e.NewSize.Height);
    }

    public void RefreshVisualInputPrompt()
    {
        // Extraction du texte pour le rendre dans la console
        _consoleLayout.Input.ActiveLine.Clear();
        Entity entity = _ecs.NewEntity("Input prompt");

        var textBlock = entity.AddComponent<Console.Components.TextComponent>();
        textBlock.Text = _pathModule.CurrentFolder + "> ";
        _consoleLayout.Input.ActiveLine.AddLineSegment(textBlock);

        entity = _ecs.NewEntity("Input Textblock");
        textBlock = entity.AddComponent<Console.Components.TextComponent>();
        textBlock.Text = _inputAccessor.Input.Text;
        _consoleLayout.Input.ActiveLine.AddLineSegment(textBlock);
    }

}
