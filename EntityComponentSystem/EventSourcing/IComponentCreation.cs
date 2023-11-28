using EntityComponentSystem;
using EntityComponentSystem.EventSourcing;
using static EntityComponentSystem.ECS;

namespace EntityComponentSystem.EventSourcing;

public interface IComponentCreation : IEvent
{
    EntityIndex Entity { get;  set; }
    ComponentIndex Component { get; set; }
    Component CreatedComponent { get; set; }

    void ApplyTo(IdentifiableList list, TreeType treeType);
}
