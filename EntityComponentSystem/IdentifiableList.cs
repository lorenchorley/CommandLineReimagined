using EntityComponentSystem.EventSourcing;

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

        if (item.Id != _list.Count)
        {
            throw new Exception("Invalid id, must insert in order");
        }

        _list.Add(item);
    }

    public T? Get<T>(int id) where T : class, IIdentifiable
    {
        return _list[id] as T;
    }

    public Entity Get(EntityAccessor accessor)
    {
        return (Entity)_list[accessor.Id];
    }
    
    public void Unset(EntityAccessor accessor)
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

    public Component Get(ComponentAccessor accessor)
    {
        return (Component)_list[accessor.Id];
    }
    
    public void Unset(ComponentAccessor accessor)
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

}
