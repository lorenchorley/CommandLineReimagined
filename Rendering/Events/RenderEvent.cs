using EntityComponentSystem;
using EntityComponentSystem.EventSourcing;
using System.Text;

namespace Rendering.Events;

public class RenderEvent : IEvent
{
    public void ApplyTo(IdentifiableList list)
    {
    }

    public void Serialise(StringBuilder sb, IdentifiableList list)
    {
        sb.AppendLine("Render");
    }
}
