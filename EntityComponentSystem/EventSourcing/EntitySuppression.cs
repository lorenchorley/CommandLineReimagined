using System.Text;
using System.Xml.Linq;
using static EntityComponentSystem.ECS;

namespace EntityComponentSystem.EventSourcing;

public class EntitySuppression : IEvent
{
    public EntityIndex Entity { get; set; } 
    public string Name { get; init; }

    public void ApplyTo(IdentifiableList list, TreeType treeType)
    {
        Entity e = list.Get(Entity);

        foreach (var component in e.Components)
        {
            component.OnDestroy();
        }

        list.Unset(Entity);
        e.Parent?.InternalRemoveChild(e);
    }

    public void Serialise(StringBuilder sb, IdentifiableList list)
    {
        sb.Append(nameof(EntitySuppression));
        sb.Append(" : ");
        sb.Append(Name);
        sb.Append('\n');
    }
}
