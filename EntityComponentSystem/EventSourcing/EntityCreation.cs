using System.Text;
using static EntityComponentSystem.ECS;

namespace EntityComponentSystem.EventSourcing;

public class EntityCreation : IEvent
{
    public EntityIndex Entity { get; set; } 
    public string Name { get; init; }
    public ECS ECS { get; init; }
    public int Id { get; init; }

    public Entity? CreatedEntity { get; private set; }

    public void ApplyTo(IdentifiableList list, TreeType treeType)
    {
        // Get the parent that should already exist
        Entity parent = list.Get(Entity);

        // Create the new entity with all the existing details, nothing new should be generated at this point so that the active tree and shadow tree are always identical
        CreatedEntity = new Entity(ECS, Id, Name, treeType);

        // Add the new entity to the parent and set the parent of the new entity
        parent.AddChild(CreatedEntity);
        CreatedEntity.InternalSetParent(parent);

        // Add the new entity to the global list
        list.Set(CreatedEntity);
    }

    public void Serialise(StringBuilder sb, IdentifiableList list)
    {
        sb.Append(nameof(EntityCreation));
        sb.Append(" : ");
        sb.Append(Name);
        sb.Append('\n');
    }
}
