using EntityComponentSystem;
using System.Text;

public class EntitySerializer
{
    private const char _indentChar = '\t';
    private readonly StringBuilder _sb;
    private int indentLevel = 0;

    public EntitySerializer()
    {
        _sb = new StringBuilder();
    }

    public string SerializeEntities(List<Entity> entities)
    {
        foreach (var e in entities)
        {
            AppendLine($"{e.Name} ({e.Id})");

            indentLevel++;
            SerializeComponents(e.Components);
            indentLevel--;
        }

        return _sb.ToString();
    }

    private void SerializeComponents(IEnumerable<Component> components)
    {
        foreach (var c in components)
        {
            AppendLine($"{c.GetType().Name} ({c.Id})");
            indentLevel++;
            foreach (var property in c.SerialisableDebugProperties)
            {
                AppendLine($"{property.Item1} : {property.Item2}");
            }
            indentLevel--;
        }
    }

    private void AppendLine(string line)
    {
        _sb.AppendLine(new string(_indentChar, indentLevel) + line);
    }
}
