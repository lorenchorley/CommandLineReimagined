using EntityComponentSystem;
using EntityComponentSystem.EventSourcing;
using System.Text;
using static EntityComponentSystem.ECS;

namespace Rendering.Events;

public class RenderEvent : IEvent
{
    public void ApplyTo(IdentifiableList list, TreeType treeType)
    {
    }

    public void Serialise(StringBuilder sb, IdentifiableList list)
    {
        sb.AppendLine("Render");
    }
}
