using EntityComponentSystem;
using EntityComponentSystem.EventSourcing;
using static EntityComponentSystem.ECS;

namespace EntityComponentSystem.EventSourcing;

public interface IComponentCreation : IEvent
{
    bool AppliedToActive { get; set; }
    bool AppliedToShadow { get; set; }
    EntityIndex Entity { get;  set; }
    ComponentIndex Component { get; set; }
    Component CreatedComponent { get; set; }

    void ApplyTo(IdentifiableList list, TreeType treeType);
}
