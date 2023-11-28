using System.ComponentModel;

namespace EntityComponentSystem.EventSourcing;

public interface IComponentProxy
{
    bool DifferentialActive { get; set; }
    Action<IEvent> RegisterDifferential { get; }
    IComponentCreation GenerateCreationEvent();
    IComponentSuppression GenerateSuppressionEvent();
}
