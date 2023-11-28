using static EntityComponentSystem.ECS;

namespace EntityComponentSystem.EventSourcing;

public interface IComponentSuppression : IComponentEvent
{
    ComponentIndex Component { get; set; }
    void ApplyTo(IdentifiableList list, TreeType treeType);
}
