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
        Entity parent = list.Get(Entity);
        CreatedEntity = new Entity(ECS, Id, Name, treeType);
        CreatedEntity.Parent = parent;
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
