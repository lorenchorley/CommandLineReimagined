using EntityComponentSystem.EventSourcing;
using System.Reflection;
using System.Text;

namespace EntityComponentSystem.Serialisation;

public class EventSourceSerialiser
{
    public string SerialiseEventSource(IEvent[] events, IdentifiableList list)
    {
        StringBuilder sb = new();

        for (int i = 0; i < events.Length; i++)
        {
            events[i].Serialise(sb, list);
        }

        return sb.ToString();
    }

    public string SerialiseEntityComponentTree(Entity root)
    {
        StringBuilder sb = new();

        InternalSerialiseEntityComponentTree(sb, "", root);

        return sb.ToString();
    }

    private void InternalSerialiseEntityComponentTree(StringBuilder sb, string indent, Entity entity)
    {
        sb.Append(indent);
        sb.Append('<');
        sb.Append(entity.Name);
        sb.Append(" Id=");
        sb.Append(entity.Id);
        sb.AppendLine(">");

        foreach (var component in entity.Components)
        {
            InternalSerialiseComponent(sb, indent + "  ", component);
        }

        foreach (var child in entity.Children)
        {
            InternalSerialiseEntityComponentTree(sb, indent + "  ", child);
        }

        sb.Append(indent);
        sb.Append("</");
        sb.Append(entity.Name);
        sb.AppendLine(">");
    }

    private void InternalSerialiseComponent(StringBuilder sb, string indent, Component component)
    {
        sb.Append(indent);
        sb.Append('[');
        sb.Append(component.GetType().Name);
        sb.Append(' ');

        var properties = component.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var attributes = property.GetCustomAttributes(typeof(StateAttribute), true);

            if (attributes.Length == 0)
            {
                continue;
            }

            var value = property.GetValue(component);

            sb.Append(property.Name);
            sb.Append('=');
            sb.Append(value?.ToString());
            sb.Append(' ');

        }

        sb.AppendLine("/]");
    }
}
