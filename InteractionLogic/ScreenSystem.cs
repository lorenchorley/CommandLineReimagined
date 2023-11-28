using EntityComponentSystem;
using System.Numerics;

namespace InteractionLogic;
public class ScreenSystem : IECSSystem, ICanvasEventEmitter
{
    private bool isInitialised = false;
    private int _actualWidth = -1;
    private int _actualHeight = -1;

    private Action<int, int>? _setCanvasSize;

    public void OnInit()
    {
    }

    public void OnStart()
    {
    }

    public Vector2 GetScreenSize()
    {
        if (!isInitialised)
        {
            throw new InvalidOperationException("ScreenSystem has not yet been initialised.");
        }

        return new Vector2(_actualWidth, _actualHeight);
    }

    public void SetSize(int actualWidth, int actualHeight)
    {
        isInitialised = true;
        _actualWidth = actualWidth;
        _actualHeight = actualHeight;

        _setCanvasSize?.Invoke(_actualWidth, _actualHeight);
    }

    public void RegisterSizeUpdateHandler(Action<int, int> setCanvasSize)
    {
        _setCanvasSize = setCanvasSize;

        if (isInitialised)
        {
            _setCanvasSize(_actualWidth, _actualHeight);
        }
    }
}
