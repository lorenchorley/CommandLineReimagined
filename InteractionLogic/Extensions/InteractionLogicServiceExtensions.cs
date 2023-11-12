using InteractionLogic;
using InteractionLogic.EventHandlers;
using InteractionLogic.FrameworkAccessors;
using Microsoft.Extensions.DependencyInjection;
using Rendering;

public static class InteractionLogicServiceExtensions
{
    public static void AddInteractionLogicServices(this IServiceCollection services)
    {
        services.AddSingleton<CanvasAccessor>();
        services.AddSingleton<InputAccessor>();
        services.AddSingleton<ContextMenuAccessor>();

        services.AddSingleton<CanvasInteractionEventHandler>();
        services.AddSingleton<CanvasRenderingEventHandler>();
        services.AddSingleton<CanvasUpdateHandler>();
        services.AddSingleton<TextInputHandler>();
        services.AddSingleton<TextInputUpdateHandler>();
    }

    public static void InitialiseInteractionLogicServices(this IServiceProvider provider)
    {
        provider.GetRequiredService<CanvasInteractionEventHandler>();
        provider.GetRequiredService<CanvasRenderingEventHandler>();
        provider.GetRequiredService<TextInputHandler>();
        provider.GetRequiredService<TextInputUpdateHandler>();

        var canvasUpdateHandler = provider.GetRequiredService<CanvasUpdateHandler>();
        var renderLoop = provider.GetRequiredService<RenderLoop>();

        renderLoop.SetRenderToScreenAction(canvasUpdateHandler.UpdateVisual);
    }
}
