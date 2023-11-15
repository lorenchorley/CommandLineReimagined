using EntityComponentSystem.EventSourcing;
using EntityComponentSystem.Serialisation;
using System.Collections.Concurrent;

namespace EntityComponentSystem;

public sealed class ECS
{
    public class ShadowECS
    {
        private Entity _shadowRoot { get; init; }
        private IdentifiableList _allShadowEntitiesAndComponentsById { get; init; }

        public IEnumerable<Entity> Entities => _allShadowEntitiesAndComponentsById.Entities;
        public IEnumerable<Component> Components => _allShadowEntitiesAndComponentsById.Components;

        public ShadowECS(Entity shadowRoot, IdentifiableList allShadowEntitiesAndComponentsById)
        {
            _shadowRoot = shadowRoot;
            _allShadowEntitiesAndComponentsById = allShadowEntitiesAndComponentsById;
        }

        public Entity? SearchForEntity(string name)
        {
            return _allShadowEntitiesAndComponentsById.Entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));
        }

        public TComponent? SearchForEntityWithComponent<TComponent>(string name) where TComponent : Component
        {
            var entity = _allShadowEntitiesAndComponentsById.Entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));

            if (entity == null)
            {
                return null;
            }

            if (!entity.TryGetComponent<TComponent>(out var component))
            {
                return null;
            }

            return component;
        }

    }

    // TODO Transform this into an event sourcing system based on the generated difference objects
    private readonly Entity _activeRoot;
    private readonly IdentifiableList _allActiveEntitiesAndComponentsById = new();
    private readonly Entity _shadowRoot;
    private readonly IdentifiableList _allShadowEntitiesAndComponentsById = new();

    private ConcurrentQueue<IEvent> _unsynchronisedActiveEvents = new();
    private ConcurrentQueue<IEvent> _shadowHistory = new();

    private static readonly object _lockMainListAccess = new();
    private int _idCounter = 0;

    public int NewId => _idCounter++;

    internal IServiceProvider ServiceProvider { get; }

    public IEnumerable<Entity> Entities => _allActiveEntitiesAndComponentsById.Entities;

    public ECS(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;

        _activeRoot = new Entity(this, NewId, "Root");
        _allActiveEntitiesAndComponentsById.Set(_activeRoot);

        _shadowRoot = new Entity(this, _activeRoot.Id, "Root");
        _allShadowEntitiesAndComponentsById.Set(_shadowRoot);
    }

    public Entity NewEntity(string name, Entity? parent = null)
    {
        parent ??= _activeRoot;

        EntityCreation creation;
        lock (_lockMainListAccess)
        {
            creation = new()
            {
                ECS = this,
                Name = name,
                Id = NewId,
                Entity = new EntityAccessor(parent)
            };
            _unsynchronisedActiveEvents.Enqueue(creation);
        }

        Apply(creation);

        return creation.CreatedEntity!;
    }

    public void RemoveEntity(Entity entity)
    {
        EntitySuppression suppression;
        lock (_lockMainListAccess)
        {
            suppression = new()
            {
                Entity = new EntityAccessor() { Id = entity.Id }
            };
            _unsynchronisedActiveEvents.Enqueue(suppression);
        }
        Apply(suppression);
    }

    private Queue<Component> _componentsToStart = new();

    public void Update()
    {
        foreach (var component in _componentsToStart)
        {
            component.OnStart();
        }

        _componentsToStart.Clear();


    }

    private void Serialise()
    {
        EventSourceSerialiser serialiser = new();

        var shadowEventSource = serialiser.SerialiseEventSource(_shadowHistory.ToArray(), _allShadowEntitiesAndComponentsById);
        var file = Path.GetFullPath("EventSourceHistory.txt");
        File.WriteAllText(file, shadowEventSource);

        var activeTree = serialiser.SerialiseEntityComponentTree(_activeRoot);
        file = Path.GetFullPath("ActiveTree.txt");
        File.WriteAllText(file, activeTree);

        var shadowTree = serialiser.SerialiseEntityComponentTree(_shadowRoot);
        file = Path.GetFullPath("ShadowTree.txt");
        File.WriteAllText(file, shadowTree);
    }

    internal void RegisterNewComponent(Component component)
    {
        _allActiveEntitiesAndComponentsById.Set(component);

        // Il faut lancer les OnStart de ces comonents qui veinnent d'être créés
        _componentsToStart.Enqueue(component);
    }

    //internal void RegisterNewEntity(Entity entity)
    //{
    //    _allActiveEntitiesAndComponentsById.Insert(entity);
    //}

    public void RegisterEvent(IEvent @event)
    {
        _unsynchronisedActiveEvents.Enqueue(@event);
    }

    public void Apply(IEvent @event)
    {
        @event.ApplyTo(_allActiveEntitiesAndComponentsById);
    }

    public ShadowECS TriggerMerge()
    {
        while (_unsynchronisedActiveEvents.TryDequeue(out var modification))
        {
            modification.ApplyTo(_allShadowEntitiesAndComponentsById);
            _shadowHistory.Enqueue(modification);
        }

        // TODO coherence verification if possible

        Serialise();

        return new ShadowECS(_shadowRoot, _allShadowEntitiesAndComponentsById);
    }

    public void AccessEntityTree(Action<IEnumerable<Entity>> callback)
    {
        callback(_activeRoot.Children);
    }

    public T AccessEntityTree<T>(Func<IEnumerable<Entity>, T> callback)
    {
        return callback(_activeRoot.Children);
    }

    private readonly Dictionary<Type, Func<int, Component>> _proxyComponentFactories = new();

    public void RegisterProxyComponent<T>(Func<int, T> factory) where T : Component, new()
    {
        _proxyComponentFactories.Add(typeof(T), factory);
    }

    internal Component? CreateProxyComponent(Type componentType)
    {
        if (!_proxyComponentFactories.TryGetValue(componentType, out var factory))
        {
            return null;
        }

        return factory(NewId);
    }

    public Entity? SearchForEntity(string name)
    {
        return Entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));
    }

    public TComponent? SearchForEntityWithComponent<TComponent>(string name) where TComponent : Component
    {
        var entity = Entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));

        if (entity == null)
        {
            return null;
        }

        if (!entity.TryGetComponent<TComponent>(out var component))
        {
            return null;
        }

        return component;
    }

}
