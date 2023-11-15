namespace EntityComponentSystem.EventSourcing;

public struct EntityAccessor : IIdentifiable
{
    public int Id { get; init; }

    public EntityAccessor(int id)
    {
        Id = id;
    }

    public EntityAccessor(Entity entity)
    {
        Id = entity.Id;
    }
}
