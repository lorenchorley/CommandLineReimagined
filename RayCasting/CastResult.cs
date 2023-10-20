
namespace EntityComponentSystem.RayCasting;

public class CastResult
{
    public RayType Type { get; init; }
    public InteractableElementLayer Layer { get; init; }
    public Entity? Entity { get; init; }
}
