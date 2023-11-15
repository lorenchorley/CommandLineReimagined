using System.Text;

namespace EntityComponentSystem.EventSourcing;

public class EntitySuppression : IEvent
{
    public EntityAccessor Entity { get; set; } 
    public void ApplyTo(IdentifiableList list)
    {
        Entity e = list.Get(Entity);

        foreach (var component in e.Components)
        {
            component.OnDestroy();
        }

        list.Unset(Entity);
    }

    public void Serialise(StringBuilder sb, IdentifiableList list)
    {
        throw new NotImplementedException();
    }
}
