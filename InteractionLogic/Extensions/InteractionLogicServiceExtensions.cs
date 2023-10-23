using InteractionLogic;
using Microsoft.Extensions.DependencyInjection;

public static class InteractionLogicServiceExtensions
{
    public static void AddInteractionLogicServices(this IServiceCollection services)
    {
        services.AddSingleton<CanvasAccessor>();
        services.AddSingleton<InputAccessor>();

        services.AddSingleton<CanvasInteractionEventHandler>();
        services.AddSingleton<CanvasRenderingEventHandler>();
        services.AddSingleton<CanvasUpdateHandler>();
        services.AddSingleton<TextInputHandler>();
    }
}
