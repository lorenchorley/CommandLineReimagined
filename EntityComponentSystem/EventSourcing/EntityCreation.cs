using System.Text;

namespace EntityComponentSystem.EventSourcing;

public class EntityCreation : IEvent
{
    public EntityAccessor Entity { get; set; } 
    public string Name { get; init; }
    public ECS ECS { get; init; }
    public int Id { get; init; }

    public Entity? CreatedEntity { get; private set; }

    public void ApplyTo(IdentifiableList list)
    {
        Entity parent = list.Get(Entity);
        CreatedEntity = new Entity(ECS, Id, Name);
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
