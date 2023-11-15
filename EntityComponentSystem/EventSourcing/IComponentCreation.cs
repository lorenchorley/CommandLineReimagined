using EntityComponentSystem;
using EntityComponentSystem.EventSourcing;

namespace EntityComponentSystem.EventSourcing;

public interface IComponentCreation : IEvent
{
    EntityAccessor Entity { set; }
    void ApplyTo(IdentifiableList list);
}
