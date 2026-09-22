using EntityComponentSystem;
using EntityComponentSystem.EventSourcing;
using static EntityComponentSystem.ECS;

namespace EntityComponentSystem.EventSourcing;

public interface IComponentDifferential : IComponentEvent
{
    new ComponentIndex Component { get; set; }
    new void ApplyTo(IdentifiableList list, TreeType treeType);
}
