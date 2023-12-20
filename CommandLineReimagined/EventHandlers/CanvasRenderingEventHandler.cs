using Application.FrameworkAccessors;
using EntityComponentSystem;
using InteractionLogic;
using System.Windows;
using System.Windows.Controls;

namespace Application.EventHandlers;

public class CanvasRenderingEventHandler : IECSSubsystem
{
    private readonly CanvasAccessor _canvasAccessor;
    private readonly ScreenSystem _screenSystem;

    public CanvasRenderingEventHandler(CanvasAccessor canvasAccessor, ScreenSystem screenSystem)
    {
        _canvasAccessor = canvasAccessor;
        _screenSystem = screenSystem;
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
        _screenSystem.SetSize((int)_canvasAccessor.Canvas!.ActualWidth, (int)_canvasAccessor.Canvas!.ActualHeight);
    }

    public void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _screenSystem.SetSize((int)_canvasAccessor.Canvas!.ActualWidth, (int)_canvasAccessor.Canvas!.ActualHeight);
    }

    public void OnInit()
    {
    }

    public void OnStart()
    {
    }
}
