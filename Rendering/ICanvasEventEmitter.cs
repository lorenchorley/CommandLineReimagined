namespace InteractionLogic;

public interface ICanvasEventEmitter
{
    void RegisterSizeUpdateHandler(Action<int, int> setCanvasSize);
}