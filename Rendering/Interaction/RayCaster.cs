using System.Drawing;
using CommandLineReimagine.Console.Components;
using CommandLineReimagine.Console.Interaction;

namespace EntityComponentSystem.Interaction
{
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
        private HitBox[] GetHitBoxes()
            => _ecs.RegisteredEntities
                   .SelectMany(x => x.Components)
                   .OfType<HitBox>()
                   .ToArray();

        public CastResult CastRay(Point targetPoint, InteractableElementLayer layer)
        {
            var entity =
                GetHitBoxes()
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
}
