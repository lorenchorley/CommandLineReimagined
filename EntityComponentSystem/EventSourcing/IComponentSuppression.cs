using EntityComponentSystem;
using EntityComponentSystem.EventSourcing;

namespace EntityComponentSystem.EventSourcing;

public interface IComponentSuppression : IComponentEvent
{
    ComponentAccessor Component { set; }
    void ApplyTo(IdentifiableList list);
}
