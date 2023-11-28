namespace EntityComponentSystem.EventSourcing;

public interface IComponentEvent : IEvent
{
    ComponentIndex Component { get; set; } // TODO Better way to reference ? By index in flat list
}
