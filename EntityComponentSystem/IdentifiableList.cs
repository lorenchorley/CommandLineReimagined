using EntityComponentSystem.EventSourcing;
using System.Text;

namespace EntityComponentSystem;

public class IdentifiableList
{
    private List<IIdentifiable?> _list = new();

    public void Set(IIdentifiable item)
    {
        if (item.Id > _list.Count)
        {
            _list.AddRange(Enumerable.Repeat<IIdentifiable?>(null, item.Id - _list.Count));
        }

        if (item.Id > _list.Count)
        {
            throw new Exception("Invalid id, must insert in order");
        }

        if (item.Id == _list.Count)
        {
            _list.Add(item);
        }
        else
        {
            if (_list[item.Id] != null)
            {
                throw new Exception("Invalid id, id has already been used");
            }

            _list[item.Id] = item;
        }

        // Garde-fou
        int v = _list.IndexOf(item);
        if (v != item.Id)
        {
            throw new Exception("Invalid id, must insert in order");
        }
    }

    public T? Get<T>(int id) where T : class, IIdentifiable
    {
        return _list[id] as T;
    }

    public Entity Get(EntityIndex accessor)
    {
        if (accessor.Id >= _list.Count)
        {
            throw new Exception("the entity has not yet been created");
        }

        IIdentifiable? identifiable = _list[accessor.Id];

        if (identifiable == null)
        {
            throw new Exception("the entity has not yet been created");
        }

        if (identifiable is not Entity entity)
        {
            throw new Exception("the item is not an entity");
        }

        return entity;
    }

    public void Unset(EntityIndex accessor)
    {
        if (_list[accessor.Id] == null)
        {
            throw new Exception("the item has already been deleted");
        }

        if (accessor.Id <= _list.Count)
        {
            throw new Exception("Invalid id, id has not yet been used");
        }

        _list[accessor.Id] = null;
    }

    public Component Get(ComponentIndex accessor)
    {
        if (accessor.Id >= _list.Count)
        {
            throw new Exception("the component has not yet been created");
        }

        IIdentifiable? identifiable = _list[accessor.Id];

        if (identifiable == null)
        {
            throw new Exception("the component has not yet been created");
        }

        if (identifiable is not Component component)
        {
            throw new Exception("the item is not a component");
        }

        return component;
    }

    public void Unset(ComponentIndex accessor)
    {
        if (_list[accessor.Id] == null)
        {
            throw new Exception("the item has already been deleted");
        }

        if (accessor.Id <= _list.Count)
        {
            throw new Exception("Invalid id, id has not yet been used");
        }

        _list[accessor.Id] = null;
    }

    public IEnumerable<Entity> Entities => _list.OfType<Entity>();
    public IEnumerable<Component> Components => _list.OfType<Component>();

    public int Count<T>()
        => _list.OfType<T>()
                .Count();

    public string SerialiseIdentifiableList()
    {
        StringBuilder sb = new();

        for (int i = 0; i < _list.Count; i++)
        {
            var item = _list[i];

            // Add the index in the format 001, 002, 003, etc.

            sb.Append(i.ToString("000"));

            sb.Append(" : ");
            sb.AppendLine(item == null ? "null" : item.GetType().Name);
        }

        return sb.ToString();
    }
}
