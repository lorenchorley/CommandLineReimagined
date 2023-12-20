using EntityComponentSystem.EventSourcing;
using EntityComponentSystem.Serialisation;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using static EntityComponentSystem.ECS;

namespace EntityComponentSystem;

public sealed partial class ECS
{

    public static ECS Instance { get; set; }

    public static void ComponentCreationCheck(string componentType, EntityIndex Entity)
    {
        Entity e = Instance._allActiveEntitiesAndComponentsById.Get(Entity);
        bool isInput = e.Name == "Input" && componentType.StartsWith("Renderer");
    }

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
    private ConcurrentQueue<IEvent> _activeHistory = new();
    private ConcurrentQueue<IEvent> _shadowHistory = new();

    private static readonly object _lockMainListAccess = new();
    private int _idCounter = 0;

    public int NewId => _idCounter++;

    internal IServiceProvider ServiceProvider { get; }

    public Entity RootEntity => _activeRoot;
    public IEnumerable<Entity> FlatEntityList => _allActiveEntitiesAndComponentsById.Entities;
    public IEnumerable<Component> FlatComponentList => _allActiveEntitiesAndComponentsById.Components;

    public ECS(IServiceProvider serviceProvider)
    {
        Instance = this;

        ServiceProvider = serviceProvider;

        _activeRoot = new Entity(this, NewId, "Root", TreeType.Active);
        _allActiveEntitiesAndComponentsById.Set(_activeRoot);

        _shadowRoot = new Entity(this, _activeRoot.Id, "Root", TreeType.Shadow);
        _allShadowEntitiesAndComponentsById.Set(_shadowRoot);

        //File.Delete("RegisteredEvents.txt");
        //File.Delete("ShadowEvents.txt");
    }

    public Entity NewEntity(string name, Entity? parent = null)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException("name");

        parent ??= _activeRoot;

        EntityCreation creation = new()
        {
            ECS = this,
            Name = name,
            Id = NewId,
            Entity = new EntityIndex(parent)
        };

        RegisterAndApplyEvent(creation);

        return creation.CreatedEntity!;
    }

    public void RemoveEntity(Entity entity)
    {
        EntitySuppression suppression = new()
        {
            Entity = new EntityIndex() { Id = entity.Id }
        };

        RegisterAndApplyEvent(suppression);
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

        var activeEventSource = serialiser.SerialiseEventSourceHistory(_activeHistory.ToArray(), _allActiveEntitiesAndComponentsById);
        File.WriteAllText(Path.GetFullPath("EventHistory_Active.txt"), activeEventSource);

        var shadowEventSource = serialiser.SerialiseEventSourceHistory(_shadowHistory.ToArray(), _allShadowEntitiesAndComponentsById);
        File.WriteAllText(Path.GetFullPath("EventHistory_Shadow.txt"), shadowEventSource);

        var activeList = _allActiveEntitiesAndComponentsById.SerialiseObjectList();
        File.WriteAllText(Path.GetFullPath("ObjectList_Active.txt"), activeList);

        var shadowList = _allShadowEntitiesAndComponentsById.SerialiseObjectList();
        File.WriteAllText(Path.GetFullPath("ObjectList_Shadow.txt"), shadowList);

        var activeTree = serialiser.SerialiseEntityComponentTree(_activeRoot);
        File.WriteAllText(Path.GetFullPath("Tree_Active.txt"), activeTree);

        var shadowTree = serialiser.SerialiseEntityComponentTree(_shadowRoot);
        File.WriteAllText(Path.GetFullPath("Tree_Shadow.txt"), shadowTree);

    }

    internal void RegisterNewComponent(IComponentCreation creation)
    {
        // Il faut lancer les OnStart de ces comonents qui veinnent d'être créés
        _componentsToStart.Enqueue(creation.CreatedComponent);
    }

    public void RegisterAndApplyEvent(IEvent @event)
    {
        RegisterEvent(@event);
        Apply(@event);
    }

    public void RegisterEvent(IEvent @event)
    {
        bool processed = false;
        while (!processed)
        {
            try
            {
                //File.AppendAllText("RegisteredEvents.txt", $"{activeCounter++} : {@event.GetType().Name} ({GetId(@event)})\n");
                processed = true;
            }
            catch (IOException)
            {

            }
        }

        _activeHistory.Enqueue(@event);
        _unsynchronisedActiveEvents.Enqueue(@event);
    }

    private static string GetId(IEvent @event)
    {
        return @event switch
        {
            EntityCreation e => $"Entity : {e.Entity.Id:000}",
            EntityDifferential e => $"Entity : {e.Entity.Id:000}",
            EntitySuppression e => $"Entity : {e.Entity.Id:000}",
            IComponentCreation e => $"Entity : {e.Entity.Id:000}",
            IComponentSuppression e => $"Component : {e.Component.Id:000}",
            IComponentDifferential e => $"Component : {e.Component.Id:000}",
            _ => @event.GetType().Name
        };
    }

    public void Apply(IEvent @event)
    {
        @event.ApplyTo(_allActiveEntitiesAndComponentsById, TreeType.Active);

        if (@event is IComponentCreation c)
        {
            if (c.AppliedToActive)
            {
                throw new InvalidOperationException("Component creation event has already been applied to the active tree");
            }
            c.AppliedToActive = true;
        }
    }

    static int shadowCounter = 0;
    static int activeCounter = 0;

    public ShadowECS TriggerMerge()
    {
        while (_unsynchronisedActiveEvents.TryDequeue(out var @event))
        {
            //File.AppendAllText("ShadowEvents.txt", $"{shadowCounter++} : {@event.GetType().Name} ({GetId(@event)})\n");

            @event.ApplyTo(_allShadowEntitiesAndComponentsById, TreeType.Shadow);

            if (@event is IComponentCreation c)
            {
                if (c.AppliedToShadow)
                {
                    throw new InvalidOperationException("Component creation event has already been applied to the shadow tree");
                }
                c.AppliedToShadow = true;
            }

            _shadowHistory.Enqueue(@event);
        }

        // TODO coherence verification if possible
        Serialise();

        return new ShadowECS(_shadowRoot, _allShadowEntitiesAndComponentsById);
    }

    public void AccessEntityTree(Action<Entity[]> callback)
    {
        callback(_activeRoot.Children.ToArray());
    }

    public T AccessEntityTree<T>(Func<Entity[], T> callback)
    {
        return callback(_activeRoot.Children.ToArray());
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
        return FlatEntityList.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));
    }

    public TComponent? SearchForEntityWithComponent<TComponent>(string name) where TComponent : Component
    {
        var entity = FlatEntityList.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));

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

    public TComponent[] GetComponents<TComponent>() where TComponent : Component
        => FlatComponentList.OfType<TComponent>().ToArray();

    //public static IEnumerable<TResult> IsType<TResult>(IEnumerable source)
    //{
    //    if (source == null)
    //    {
    //        ArgumentNullException.ThrowIfNull(source, nameof(source));
    //    }

    //    return OfTypeIterator<TResult>(source);
    //}

    //private static IEnumerable<TResult> OfTypeIterator<TResult>(IEnumerable source)
    //{
    //    foreach (object? obj in source)
    //    {
    //        if (obj is TResult result)
    //        {
    //            yield return result;
    //        }
    //    }
    //}

}
