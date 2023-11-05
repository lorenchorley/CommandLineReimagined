using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace EntityComponentSystem
{
    public class Entity
    {
        public int Id { get; private init; }
        public string Name { get; init; }

        [JsonIgnore]
        public ECS ECS { get; init; }
        // Unique
        // Encapsulate
        private List<Component> _components { get; } = new();

        public Entity(ECS ecs, int id)
        {
            ECS = ecs;
            Id = id;
        }

        public void Destroy()
        {
            foreach (var component in _components)
            {
                component.OnDestroy();
            }

            _components.Clear();

            ECS.RemoveEntity(this);
            //if (!ECS.RemoveEntityr(this))
            //{
            //    throw new InvalidOperationException("Entity was not registered at the moment when its Destroy method was called");
            //}
        }

        public IEnumerable<Component> Components => _components;

        public T? TryGetComponent<T>() where T : Component
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
            component.Entity = null;
        }

        public void RemoveComponent(Component component)
        {
            if (!_components.Contains(component))
            {
                throw new InvalidOperationException($"Component of type {component.GetType().Name} does not exist");
            }

            _components.Remove(component);
            component.Entity = null;
        }

        public T AddComponent<T>() where T : Component, new()
        {
            if (HasComponent<T>())
            {
                throw new InvalidOperationException($"Component of type {typeof(T).Name} already exists");
            }

            T component = new()
            {
                Id = ECS.NewId
            };
            _components.Add(component);
            component.Entity = this;
            return component;
        }

        public T AddComponent<T>(Action<T> configure) where T : Component, new()
        {
            var component = AddComponent<T>();
            configure(component);
            return component;
        }

        public T TryAddComponent<T>() where T : Component, new()
        {
            if (TryGetComponent(out T? component))
                return component;

            return AddComponent<T>();
        }
    }
}
