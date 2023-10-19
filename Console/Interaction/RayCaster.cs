using System.Drawing;
using System.Linq;
using CommandLineReimagine.Console.Components;

namespace CommandLineReimagine.Console.Interaction
{
    public class RayCaster
    {
        private readonly EntityComponentSystem _ecs;

        public RayCaster(EntityComponentSystem ecs)
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
