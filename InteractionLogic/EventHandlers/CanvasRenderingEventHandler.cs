using CommandLine.Modules;
using Commands.Parser;
using Console;
using Console.Components;
using EntityComponentSystem;
using EntityComponentSystem.RayCasting;
using InteractionLogic.FrameworkAccessors;
using Rendering;
using System.Windows;
using System.Windows.Controls;
using Terminal;
using Terminal.Commands.Parser.Serialisation;

namespace InteractionLogic;

public class CanvasRenderingEventHandler
{
    private readonly CanvasAccessor _canvasAccessor;
    private readonly InputAccessor _inputAccessor;
    private readonly RenderLoop _renderLoop;
    private readonly ECS _ecs;
    private readonly PathModule _pathModule;
    private readonly Prompt _prompt;

    public CanvasRenderingEventHandler(CanvasAccessor canvasAccessor, InputAccessor inputAccessor, RenderLoop renderLoop, ECS ecs, PathModule pathModule, Prompt prompt)
    {
        _canvasAccessor = canvasAccessor;
        _inputAccessor = inputAccessor;
        _renderLoop = renderLoop;
        _ecs = ecs;
        _pathModule = pathModule;
        _prompt = prompt;
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

        _prompt.SetCursorPosition(_inputAccessor.Input.SelectionStart);
        _prompt.InitPromptText();
    }

    public void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        //_buffer.SetSize(e.NewSize.Width, e.NewSize.Height);
    }


}
