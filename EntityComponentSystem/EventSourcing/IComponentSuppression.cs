using static EntityComponentSystem.ECS;

namespace EntityComponentSystem.EventSourcing;

public interface IComponentSuppression : IComponentEvent
{
    new ComponentIndex Component { get; set; }
    new void ApplyTo(IdentifiableList list, TreeType treeType);
}
