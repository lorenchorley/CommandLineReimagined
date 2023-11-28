namespace EntityComponentSystem.EventSourcing;

public struct ComponentIndex : IIdentifiable
{
    public int Id { get; init; } = -1;

    public ComponentIndex(int id)
    {
        Id = id;
    }

    public ComponentIndex(Component component)
    {
        Id = component.Id;
    }
}
