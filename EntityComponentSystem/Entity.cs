using EntityComponentSystem.Attributes;
using EntityComponentSystem.EventSourcing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

namespace EntityComponentSystem;

public sealed class Entity : IIdentifiable
{
    public int Id { get; private init; }
    public readonly string Name;
    private readonly TreeType _treeType;

    [JsonIgnore]
    public ECS ECS { get; init; }
    private List<Component> _components { get; } = new();
    private List<Entity> _children { get; } = new();

    public Entity? _parent;
    public Entity? Parent
    {
        get
        {
            return _parent;
        }
        set
        {
            if (_parent != null)
            {
                _parent.RemoveChild(this);
            }

            _parent = value;

            if (_parent != null)
            {
                _parent.AddChild(this);
            }
        }
    }

    public Entity(ECS ecs, int id, string name, TreeType treeType)
    {
        ECS = ecs;
        Id = id;
        Name = name;
        _treeType = treeType;
    }

    #region Entity functions
    public void Destroy()
    {
        foreach (var component in _components)
        {
            component.Destroy();
        }

        _components.Clear();

        ECS.RemoveEntity(this);
        //if (!ECS.RemoveEntity(this))
        //{
        //    throw new InvalidOperationException("Entity was not registered at the moment when its Destroy method was called");
        //}
    }
    #endregion

    #region Child functions
    private readonly object _lockChildren = new();

    public Span<Entity> Children
    {
        get
        {
            lock (_lockChildren)
            {
                return CollectionsMarshal.AsSpan(_children);
            }
        }
    }

    internal void AddChild(Entity child)
    {
        lock (_lockChildren)
        {
            _children.Add(child);
        }
    }

    internal void RemoveChild(Entity child)
    {
        lock (_lockChildren)
        {
            _children.Remove(child);
        }
    }
    #endregion

    #region Component functions
    private readonly object _lockComponents = new();

    public Span<Component> Components
    {
        get
        {
            lock (_lockComponents)
            {
                return CollectionsMarshal.AsSpan(_components);
            }
        }
    }

    public Component AddComponent(Type componentType)
    {
        lock (_lockComponents)
        {
            if (HasComponent(componentType))
            {
                throw new InvalidOperationException($"Component of type {componentType.Name} already exists");
            }

            // Find type in the same assembly as componentType, but with Proxy at the end
            var creationType = componentType.Assembly.GetType($"{componentType.Namespace}.{componentType.Name}Creation");
            IComponentCreation? creation = (IComponentCreation)Activator.CreateInstance(creationType);
            creation.Entity = new(this);
            creation.Component = new(ECS.NewId);

            ECS.RegisterEvent(creation);
            ECS.RegisterNewComponent(creation);

            return creation.CreatedComponent;
        }
    }

    public Component InternalAddComponent(Type proxyType, ComponentIndex componentIndex, TreeType treeType)
    {
        // Otherwise just create a new instance
        Component component = (Component)(Activator.CreateInstance(proxyType) ?? throw new InvalidOperationException($"Activator.CreateInstance({proxyType.Name})"));
        IComponentProxy proxy = (IComponentProxy)component;

        var idProp = component.GetType().GetProperty(nameof(component.Id)) ?? throw new NullReferenceException(nameof(component.Id));
        idProp.SetValue(component, componentIndex.Id);

        var treeTypeProp = component.GetType().GetProperty(nameof(component.TreeType)) ?? throw new NullReferenceException(nameof(component.Id));
        treeTypeProp.SetValue(component, treeType);

        var registerDifferentialProp = component.GetType().GetProperty(nameof(proxy.RegisterDifferential)) ?? throw new NullReferenceException(nameof(proxy.RegisterDifferential));
        registerDifferentialProp.SetValue(component, (object)ECS.RegisterEvent);
        
        if (treeType == TreeType.Shadow)
        {
            // Désactivation des differentiels dans le shadow tree
            var differentialActiveProp = component.GetType().GetProperty(nameof(proxy.DifferentialActive)) ?? throw new NullReferenceException(nameof(proxy.DifferentialActive));
            differentialActiveProp.SetValue(component, false);
        }

        component.Init(this);

        _components.Add(component);

        InjectDependencies(proxyType, component);
        SetupStateListeners(proxyType);

        component.OnInit();

        return component;
    }

    public T AddComponent<T>() where T : Component, new()
    {
        return (T)AddComponent(typeof(T));
    }

    private void SetupStateListeners(Type componentType)
    {

    }

    private void InjectDependencies(Type componentType, Component component)
    {
        var properties = componentType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var attributes = property.GetCustomAttributes(typeof(InjectAttribute), true);

            if (attributes.Length == 0)
            {
                continue;
            }

            var dependency = ECS.ServiceProvider.GetRequiredService(property.PropertyType);
            property.SetValue(component, dependency);
        }
    }

    public T AddComponent<T>(Action<T> configure) where T : Component, new()
    {
        var component = AddComponent<T>();
        configure(component);
        return component;
    }

    public T GetOrAddComponent<T>() where T : Component, new()
    {
        if (TryGetComponent(out T? component))
            return component;

        return AddComponent<T>();
    }

    public T? TryGetComponent<T>() where T : Component
    {
        return Components.ToArray().OfType<T>().FirstOrDefault();
    }

    public T? TryGetComponentByInterface<T>()
    {
        return Components.ToArray().OfType<T>().FirstOrDefault();
    }

    public bool TryGetComponent<T>([NotNullWhen(true)] out T? component) where T : Component
    {
        component = Components.ToArray().OfType<T>().FirstOrDefault();
        return component != null;
    }

    public T GetComponent<T>() where T : Component
    {
        return Components.ToArray().OfType<T>().First();
    }

    public bool HasComponent(Type componentType)
    {
        return Components.ToArray().Where(s => componentType.IsSubclassOf(s.GetType())).Any(); // TODO Verify
    }

    public bool HasComponent<T>() where T : Component
    {
        return Components.ToArray().OfType<T>().Any();
    }

    public void RemoveComponent<T>() where T : Component
    {
        if (!TryGetComponent(out T? component))
        {
            throw new InvalidOperationException($"Component of type {typeof(T).Name} does not exist");
        }

        RemoveComponent(component);

        component.InternalDestroy();
    }

    // Should create event and the event should call an InternalRemoveComponent
    public void RemoveComponent(Component component)
    {
        if (!Components.ToArray().Contains(component))
        {
            throw new InvalidOperationException($"Component of type {component.GetType().Name} does not exist");
        }

        RemoveComponent(component);

        component.InternalDestroy();
    }

    private void RecordValueSet<T>(T value)
    {
        // Record the value set to the property here
    }
    #endregion
}
