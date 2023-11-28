using EntityComponentSystem;
using EntityComponentSystem.EventSourcing;
using static EntityComponentSystem.ECS;

namespace EntityComponentSystem.EventSourcing;

public interface IComponentDifferential : IComponentEvent
{
    ComponentIndex Component { get; set; }
    void ApplyTo(IdentifiableList list, TreeType treeType);
}
