using Newtonsoft.Json.Linq;
using System.Text;
using static EntityComponentSystem.ECS;

namespace EntityComponentSystem.EventSourcing;

public enum ParentalModification
{
    None,
    Set,
    Remove,
    Change
}

public class EntityDifferential : IEvent
{
    public EntityIndex Entity { get; set; }
    public ECS ECS { get; init; }

    public ParentalModification ParentalModification { get; set; }
    public EntityIndex? NewParent { get; set; }

    public void ApplyTo(IdentifiableList list, TreeType treeType)
    {
        Entity entity = list.Get(Entity);

        if (ParentalModification != ParentalModification.None)
        {
            // Update old parent by removing the entity as a child
            if (entity.Parent != null)
            {
                entity.Parent.InternalRemoveChild(entity);
            }

            switch (ParentalModification)
            {
                case ParentalModification.Set:
                    ArgumentNullException.ThrowIfNull(NewParent);

                    entity.InternalSetParent(list.Get(NewParent.Value));
                    break;
                case ParentalModification.Remove:
                    entity.InternalSetParent(null);
                    break;
                case ParentalModification.Change:
                    ArgumentNullException.ThrowIfNull(NewParent);

                    entity.InternalSetParent(list.Get(NewParent.Value));
                    break;
            }

            // Update new parent with new child
            if (entity.Parent != null)
            {
                entity.Parent.InternalAddChild(entity);
            }
        }

    }

    public void Serialise(StringBuilder sb, IdentifiableList list)
    {
        sb.Append(nameof(EntityDifferential));
        sb.Append(" : ");
        if (NewParent.HasValue)
        {
            sb.Append("Parent: ");
            sb.Append(NewParent.Value);
        }
        sb.Append('\n');
    }
}
