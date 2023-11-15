using Rendering.Components;
using Rendering.Interaction;
using System.Drawing;

namespace EntityComponentSystem.RayCasting;

public class RayCaster
{
    private readonly ECS _ecs;

    public RayCaster(ECS ecs)
    {
        _ecs = ecs;
    }

    // Don't worry about performance
    // Don't worry about performance
    // Don't worry about performance
    private InteractiveComponent[] GetInteractiveComponents()
        => _ecs.AccessEntityTree(list => 
               list.SelectMany(x => x.Components)
                   .OfType<InteractiveComponent>()
                   .ToArray()
            );

    public CastResult CastRay(Point targetPoint, InteractableElementLayer layer)
    {
        // TODO transform the point from screen space to ui space

        var entity =
            GetInteractiveComponents()
                .Select(h => h.Entity.GetComponent<Renderer>())
                .Where(r => r.IsVisible)
                .Where(x => x.CanvasRenderPosition.Contains(targetPoint))
                .Select(s => s.Entity)
                .FirstOrDefault();

        return new()
        {
            Type = RayType.PrecisePoint,
            Layer = layer,
            Entity = entity,
        };
    }
}
