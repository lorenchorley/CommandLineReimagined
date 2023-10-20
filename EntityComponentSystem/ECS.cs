namespace EntityComponentSystem
{
    public class ECS
    {
        public List<Entity> RegisteredEntities { get; private set; } = new();

        private int _idCounter = 0;

        public int NewId => _idCounter++;

        public Entity NewEntity(string name)
        {
            var entity = new Entity(this, _idCounter++)
            {
                Name = name
            };

            RegisteredEntities.Add(entity);

            return entity;
        }

    }
}
