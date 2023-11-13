using UIComponents;
using UIComponents.Components;
using EntityComponentSystem;
using Microsoft.Extensions.DependencyInjection;
using Rendering;

public static class VisualInterfaceServiceExtensions
{
    public static void AddVisualInterfaceServices(this IServiceCollection services)
    {
    }
    
    public static void InitialVisualInterfaceServices(this IServiceProvider serviceProvider)
    {
        var ecs = serviceProvider.GetRequiredService<ECS>();
        ecs.RegisterProxyComponent<TextComponent>((id) => new TextComponentProxy() { Id = id });
    }
}
