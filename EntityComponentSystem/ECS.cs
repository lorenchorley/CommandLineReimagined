namespace EntityComponentSystem
{
    public class ECS
    {
        private readonly List<Entity> _registeredEntities = new();
        private readonly List<Entity> _newEntities = new();
        private readonly List<Entity> _removedEntities = new();

        private static readonly object _lockMainListAccess = new();
        private int _idCounter = 0;

        public int NewId => _idCounter++;

        public Entity NewEntity(string name)
        {
            lock (_lockMainListAccess)
            {
                var entity = new Entity(this, _idCounter++)
                {
                    Name = name
                };

                _newEntities.Add(entity);

                return entity;
            }
        }

        public void RemoveEntity(Entity entity)
        {
            lock (_lockMainListAccess)
            {
                _removedEntities.Add(entity);
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

    }
}
