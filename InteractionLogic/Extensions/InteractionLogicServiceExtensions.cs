using InteractionLogic;
using Microsoft.Extensions.DependencyInjection;

public static class InteractionLogicServiceExtensions
{
    public static void AddInteractionLogicServices(this IServiceCollection services)
    {
        services.AddECSSingleton<InputSystem>();
        services.AddECSSingleton<ScreenSystem>();
        services.AddSingleton<ICanvasEventEmitter>(x => x.GetRequiredService<ScreenSystem>());
    }

    public static void InitialiseInteractionLogicServices(this IServiceProvider provider)
    {


    }
}
