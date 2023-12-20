using EntityComponentSystem.EventSourcing;
using System.Reflection;
using System.Text;

namespace EntityComponentSystem.Serialisation;

public class EventSourceSerialiser
{
    internal const string IndexFormat = "0000";
    private const string _indentUnit = "  ";

    public string SerialiseEventSourceHistory(IEvent[] events, IdentifiableList list)
    {
        StringBuilder sb = new();

        for (int i = 0; i < events.Length; i++)
        {
            IEvent @event = events[i];
            @event.Serialise(sb, list);
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
        sb.Append("|");
        sb.Append("Entity");
        sb.Append(" Id=");
        sb.Append(entity.Id.ToString(IndexFormat));
        sb.AppendLine(">");

        foreach (var component in entity.Components)
        {
            InternalSerialiseComponent(sb, indent + _indentUnit, component);
        }

        Span<Entity> children = entity.Children;
        for (int i = 0; i < children.Length; i++)
        {
            var child = children[i];
            if (child == null)
            {
                continue;
            }

            InternalSerialiseEntityComponentTree(sb, indent + _indentUnit, child);
        }

        sb.Append(indent);
        sb.Append("</");
        sb.Append(entity.Name);
        sb.AppendLine(">");
    }

    private void InternalSerialiseComponent(StringBuilder sb, string indent, Component component)
    {
        Type type = component.GetType();
        if (component is IComponentProxy)
        {
            type = type.BaseType ?? throw new Exception("Unexpected type structure, a component proxy does not have a base type");
        }

        sb.Append(indent);
        sb.Append('[');
        sb.Append(type.Name);

        sb.Append(' ');
        sb.Append("Id=");
        sb.Append(component.Id);


        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Dictionary<string, object> tagPropertiesByName = new();
        foreach (var property in properties)
        {
            var attributes = property.GetCustomAttributes(typeof(StateAttribute), true);
            if (attributes.Length == 0)
                continue;

            var value = property.GetValue(component);
            if (value is null)
                continue;

            if (value is string s)
            {
                sb.Append(' ');
                sb.Append(property.Name);
                sb.Append('=');
                sb.Append('"');
                sb.Append(s);
                sb.Append('"');
            }
            else if (value is System.Collections.IList list)
            {
                // Don't show empty lists
                if (list.Count == 0)
                    continue;

                tagPropertiesByName.Add(property.Name, list);
            }
            else
            {
                sb.Append(' ');
                sb.Append(property.Name);
                sb.Append('=');
                sb.Append(value.ToString());
            }
        }

        if (tagPropertiesByName.Count > 0)
        {
            sb.AppendLine("]");

            foreach (var prop in tagPropertiesByName)
            {
                sb.Append(indent);
                sb.Append(_indentUnit);

                if (prop.Value is System.Collections.IList list)
                {
                    //sb.Append('(');
                    sb.Append(prop.Key);
                    sb.AppendLine(" = [");

                    foreach (var item in list)
                    {
                        if (item is null)
                            continue;

                        sb.Append(indent);
                        sb.Append(_indentUnit);
                        sb.Append(_indentUnit);
                        sb.Append("<$");
                        sb.Append(ValueToString(item));
                        sb.AppendLine("/>");
                    }

                    sb.Append(indent);
                    sb.Append(_indentUnit);
                    sb.AppendLine("]");

                }
                else
                {

                }

            }
            sb.Append(indent);
            sb.AppendLine("[/]");
        }
        else
        {
            sb.AppendLine("/]");
        }
    }

    private string ValueToString(object value)
    {
        if (value is IComponentProxy proxy)
        {
            string id = proxy.Id.ToString(IndexFormat);
            string typeName = proxy.GetType().BaseType.Name;
            return $"{typeName} Id={id}";
        }
        else
        {
            return value.ToString();
        }
    }

}

//public struct TagSpacer
//{
//    private bool _needsSpace;
//    private StringBuilder _sb;

//    public TagSpacer(StringBuilder sb)
//    {
//        _needsSpace = false;
//        _sb = sb;
//    }

//    public void Set()
//    {
//        _needsSpace = true;
//    }

//    public void Reset()
//    {
//        _needsSpace = false;
//    }

//    /// <summary>
//    /// Add a space only when we know that we need it
//    /// </summary>
//    public void AppendSpaceIfNeeded()
//    {
//        //if (_needsSpace)
//        //{
//            _sb.Append(' ');
//        //}
//    }
//}