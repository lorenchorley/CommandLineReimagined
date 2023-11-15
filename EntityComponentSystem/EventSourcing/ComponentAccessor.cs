namespace EntityComponentSystem.EventSourcing;

public struct ComponentAccessor : IIdentifiable
{
    public int Id { get; init; }

    public ComponentAccessor(int id)
    {
        Id = id;
    }

    public ComponentAccessor(Component component)
    {
        Id = component.Id;
    }
}
