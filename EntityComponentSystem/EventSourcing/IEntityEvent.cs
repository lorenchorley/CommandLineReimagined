namespace EntityComponentSystem.EventSourcing;

public interface IEntityEvent : IEvent
{
    EntityAccessor Entity { get; set; } // TODO Better way to reference ? By index in flat list
}
