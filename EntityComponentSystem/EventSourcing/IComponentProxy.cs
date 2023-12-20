using System.ComponentModel;

namespace EntityComponentSystem.EventSourcing;

public interface IComponentProxy
{
    public int Id { get; }
    bool DifferentialActive { get; set; }
    Action<IEvent> RegisterDifferential { get; }
    //IComponentCreation GenerateCreationEvent();
    IComponentSuppression GenerateSuppressionEvent();
}
