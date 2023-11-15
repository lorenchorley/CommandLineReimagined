using System.Collections.Generic;
using System.Text;

namespace EntityComponentSystem.EventSourcing;

public interface IEvent
{
    void ApplyTo(IdentifiableList list);

    void Serialise(StringBuilder sb, IdentifiableList list);
    
}
