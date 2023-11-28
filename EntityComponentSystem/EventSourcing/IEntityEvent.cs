namespace EntityComponentSystem.EventSourcing;

public interface IEntityEvent : IEvent
{
    EntityIndex Entity { get; set; } // TODO Better way to reference ? By index in flat list
}
