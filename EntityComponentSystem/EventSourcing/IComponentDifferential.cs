using EntityComponentSystem;
using EntityComponentSystem.EventSourcing;

namespace EntityComponentSystem.EventSourcing;

public interface IComponentDifferential : IComponentEvent
{
    ComponentAccessor Component { set; }
    void ApplyTo(IdentifiableList list);
}
