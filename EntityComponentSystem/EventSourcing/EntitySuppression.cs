using System.Text;
using static EntityComponentSystem.ECS;

namespace EntityComponentSystem.EventSourcing;

public class EntitySuppression : IEvent
{
    public EntityIndex Entity { get; set; } 
    public void ApplyTo(IdentifiableList list, TreeType treeType)
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
