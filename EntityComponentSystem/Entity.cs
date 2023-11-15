using EntityComponentSystem.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace EntityComponentSystem;

public sealed class Entity : IIdentifiable
{
    public int Id { get; private init; }
    public readonly string Name;

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

    public Entity(ECS ecs, int id, string name)
    {
        ECS = ecs;
        Id = id;
        Name = name;
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
    public IEnumerable<Entity> Children => _children;

    internal void AddChild(Entity child)
    {
        _children.Add(child);
    }

    internal void RemoveChild(Entity child)
    {
        _children.Remove(child);
    }
    #endregion

    #region Component functions
    public IEnumerable<Component> Components => _components;

    public Component AddComponent(Type componentType)
    {
        if (HasComponent(componentType))
        {
            throw new InvalidOperationException($"Component of type {componentType.Name} already exists");
        }

        Component? component =
            ECS.CreateProxyComponent(componentType); // Use proxy class system

        if (component == null)
        {
            // Otherwise just create a new instance
            component = (Component)Activator.CreateInstance(componentType);
            component.GetType().GetProperty("Id").SetValue(component, ECS.NewId);
        }

        _components.Add(component);
        ECS.RegisterNewComponent(component);

        InjectDependencies(componentType, component);
        SetupStateListeners(componentType);

        component.Init(this);

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
        return _components.OfType<T>().FirstOrDefault();
    }

    public T? TryGetComponentByInterface<T>()
    {
        return _components.OfType<T>().FirstOrDefault();
    }

    public bool TryGetComponent<T>([NotNullWhen(true)] out T? component) where T : Component
    {
        component = _components.OfType<T>().FirstOrDefault();
        return component != null;
    }

    public T GetComponent<T>() where T : Component
    {
        return _components.OfType<T>().First();
    }

    public bool HasComponent(Type componentType)
    {
        return _components.Where(s => componentType.IsSubclassOf(s.GetType())).Any(); // TODO Verify
    }
    
    public bool HasComponent<T>() where T : Component
    {
        return _components.OfType<T>().Any();
    }

    public void RemoveComponent<T>() where T : Component
    {
        if (!TryGetComponent(out T? component))
        {
            throw new InvalidOperationException($"Component of type {typeof(T).Name} does not exist");
        }

        _components.Remove(component);

        component.InternalDestroy();
    }

    // Should create event and the event should call an InternalRemoveComponent
    public void RemoveComponent(Component component)
    {
        if (!_components.Contains(component))
        {
            throw new InvalidOperationException($"Component of type {component.GetType().Name} does not exist");
        }

        _components.Remove(component);

        component.InternalDestroy();
    }

    private void RecordValueSet<T>(T value)
    {
        // Record the value set to the property here
    }
    #endregion
}
