namespace EntityComponentSystem
{
    public sealed class ECS
    {
        // TODO Transform this into an event sourcing system based on the generated difference objects
        private readonly List<Entity> _registeredEntities = new();
        private readonly List<Entity> _newEntities = new();
        private readonly List<Entity> _removedEntities = new();

        private static readonly object _lockMainListAccess = new();
        private int _idCounter = 0;

        public int NewId => _idCounter++;

        internal IServiceProvider ServiceProvider { get; }

        public ECS(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public Entity NewEntity(string name)
        {
            lock (_lockMainListAccess)
            {
                var entity = new Entity(this, _idCounter++, name);

                _newEntities.Add(entity);

                return entity;
            }
        }

        public void RemoveEntity(Entity entity)
        {
            lock (_lockMainListAccess)
            {
                _removedEntities.Add(entity);

                foreach (var component in entity.Components)
                {
                    component.OnDestroy();
                }
            }
        }

        public void MergeEntities()
        {
            lock (_lockMainListAccess)
            {
                InternalMergeEntities();
            }
        }

        public void AccessEntities(Action<List<Entity>> callback)
        {
            lock (_lockMainListAccess)
            {
                InternalMergeEntities();

                callback(_registeredEntities);
            }
        }

        public T AccessEntities<T>(Func<List<Entity>, T> callback)
        {
            lock (_lockMainListAccess)
            {
                InternalMergeEntities();

                return callback(_registeredEntities);
            }
        }

        private void InternalMergeEntities()
        {
            _registeredEntities.AddRange(_newEntities);
            _newEntities.Clear();

            _removedEntities.ForEach(e => _registeredEntities.Remove(e));
            _removedEntities.Clear();
        }

        private readonly Dictionary<Type, Func<int, Component>> _proxyComponentFactories = new();

        public void RegisterProxyComponent<T>(Func<int, T> factory) where T : Component, new()
        {
            _proxyComponentFactories.Add(typeof(T), factory);
        }

        internal T CreateProxyComponent<T>() where T : Component, new()
        {
            if (!_proxyComponentFactories.TryGetValue(typeof(T), out var factory))
            {
                throw new InvalidOperationException($"No proxy component factory registered for type {typeof(T).Name}");
            }

            return (T)factory(NewId);
        }

        public Entity? SearchForEntity(string name)
        {
            return AccessEntities(list => list.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal)));
        }

        public TComponent? SearchForEntityWithComponent<TComponent>(string name) where TComponent : Component
        {
            return AccessEntities(list =>
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
}
