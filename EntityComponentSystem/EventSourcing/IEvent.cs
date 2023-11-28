using System.Collections.Generic;
using System.Text;
using static EntityComponentSystem.ECS;

namespace EntityComponentSystem.EventSourcing;

public interface IEvent
{
    void ApplyTo(IdentifiableList list, TreeType treeType);

    void Serialise(StringBuilder sb, IdentifiableList list);
    
}
