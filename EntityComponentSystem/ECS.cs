using System.Xml.Linq;

namespace EntityComponentSystem;

public struct EntityAccessor
{
    public Entity Entity { get; init; }
}

public struct ComponentAccessor
{
    public Component Component { get; init; }
}



public interface IEntityTreeModification
{
}



public interface IEntityModification : IEntityTreeModification
{
    EntityAccessor Entity { get; init; } // TODO Better way to reference ? By index in flat list
    void ApplyTo(Entity entity);
}

public interface IComponentModification : IEntityTreeModification
{
    ComponentAccessor Component { get; init; } // TODO Better way to reference ? By index in flat list
    void ApplyTo(Component component);
}



public class IEntityCreation : IEntityModification
{
    public EntityAccessor Entity { get; init; } // TODO Better way to reference ? By index in flat list

    public string Name { get; init; }
    public ECS ECS { get; init; }
    public Entity? CreatedEntity { get; private set; }

    public void ApplyTo(Entity entity)
    {
        CreatedEntity = new Entity(ECS, ECS.NewId, Name);

    }
}

public class IEntitySuppression : IEntityModification
{
    public EntityAccessor Entity { get; init; } // TODO Better way to reference ? By index in flat list
    public void ApplyTo(Entity entity)
    {

        foreach (var component in entity.Components)
        {
            component.OnDestroy();
        }
    }
}


public sealed class ECS
{
    // TODO Transform this into an event sourcing system based on the generated difference objects
    private readonly Entity _activeRoot;
    private readonly Dictionary<int, Entity> _allActiveEntitiesById = new();
    private readonly Entity _shadowRoot;
    private readonly List<Entity> _allShadowEntities = new();

    private List<IEntityTreeModification> _shadowEventsToApply = new();
    private readonly List<IEntityTreeModification> _shadowHistory = new();
    private List<IEntityTreeModification> _unshadowedActiveEvents = new();

    private static readonly object _lockMainListAccess = new();
    private int _idCounter = 0;

    public int NewId => _idCounter++;

    internal IServiceProvider ServiceProvider { get; }

    public ECS(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;

        _activeRoot = new Entity(this, NewId, "Root");
        _shadowRoot = new Entity(this, _activeRoot.Id, "Root");
    }

    public Entity NewEntity(string name, Entity? parent = null)
    {
        IEntityCreation creation;

        parent ??= _activeRoot;

        lock (_lockMainListAccess)
        {
            creation = new()
            {
                ECS = this,
                Entity = new EntityAccessor() { Entity = parent }
            };
            _unshadowedActiveEvents.Add(creation);
        }

        return creation.CreatedEntity!;
    }

    public void RemoveEntity(Entity entity)
    {
        IEntitySuppression suppression;

        lock (_lockMainListAccess)
        {
            suppression = new()
            {
                Entity = new EntityAccessor() { Entity = entity }
            };
            _unshadowedActiveEvents.Add(suppression);
        }

        suppression.ApplyTo(entity);
    }

    public void TriggerMerge()
    {
        lock (_lockMainListAccess)
        {
            List<IEntityTreeModification> swap = _unshadowedActiveEvents;
            _unshadowedActiveEvents = _shadowEventsToApply;
            _shadowEventsToApply = swap;
        }

        foreach (IEntityTreeModification modification in _shadowEventsToApply)
        {
            if (modification is IComponentModification componentModification)
            {
                componentModification.ApplyTo(componentModification.ShadowComponent.);
            }
            else if (modification is IEntityModification entityModification)
            {
                entityModification.ApplyTo(entityModification.Entity.Entity);
            }
        }

        // TODO coherence verification
    }

    public void AccessEntityTree(Action<List<Entity>> callback)
    {
        callback(_activeRoot);
    }

    public T AccessEntityTree<T>(Func<List<Entity>, T> callback)
    {
        return callback(_activeRoot);
    }

    private readonly Dictionary<Type, Func<int, Component>> _proxyComponentFactories = new();

    public void RegisterProxyComponent<T>(Func<int, T> factory) where T : Component, new()
    {
        _proxyComponentFactories.Add(typeof(T), factory);
    }

    internal T? CreateProxyComponent<T>() where T : Component, new()
    {
        if (!_proxyComponentFactories.TryGetValue(typeof(T), out var factory))
        {
            return null;
        }

        return (T)factory(NewId);
    }

    public Entity? SearchForEntity(string name)
    {
        return AccessEntityTree(list => list.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal)));
    }

    public TComponent? SearchForEntityWithComponent<TComponent>(string name) where TComponent : Component
    {
        return AccessEntityTree(list =>
        {
            var entity = list.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));

            if (entity == null)
            {
                return null;
            }

            if (!entity.TryGetComponent<TComponent>(out var component))
            {
                return null;
            }

            return component;
        });
    }
}
