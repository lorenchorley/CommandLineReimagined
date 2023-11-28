namespace EntityComponentSystem.EventSourcing;

public struct EntityIndex : IIdentifiable
{
    public int Id { get; init; } = -1;

    public EntityIndex(int id)
    {
        Id = id;
    }

    public EntityIndex(Entity entity)
    {
        Id = entity.Id;
    }
}
