using EntityComponentSystem.Serialisation;
using Newtonsoft.Json;

namespace EntityComponentSystem
{
    public abstract class Component
    {
        public int Id { get; init; }
        public bool IsDestoried { get; private set; } = false;

        private Entity? _entity;

        [JsonIgnore]
        [NonSerialisableState]
        public Entity Entity
        {
            get
            {
                ArgumentNullException.ThrowIfNull(_entity);

                return _entity;
            }
            private set
            {
                if (IsDestoried)
                {
                    throw new InvalidOperationException("Component is already destroyed.");
                }

                // On ne peut pas changer l'entité d'un composant déjà initialisé
                if (value != null && _entity != null)
                {
                    throw new InvalidOperationException("Entity is already set.");
                }

                _entity = value;

                //// Si on n'est pas en train de supprimer l'entité
                //if (value != null)
                //{
                //    // On a besoin de s'assurer que les dépendances sont satisfaites
                //    InsureDependencies();
                //}
            }
        }

        [JsonIgnore]
        [NonSerialisableState]
        public ECS ECS => Entity.ECS;

        public abstract IEnumerable<(string, string)> SerialisableDebugProperties { get; }

        protected TDependency EnsureDependency<TDependency>() where TDependency : Component, new()
        {
            return Entity.TryAddComponent<TDependency>();
        }

        public virtual void OnInit() { }
        public virtual void OnDestroy() { }

        internal void Init(Entity entity)
        {
            Entity = entity;

            OnInit();
        }

        internal void InternalDestroy()
        {
            IsDestoried = true;
            OnDestroy();
        }

        public void Destroy() 
        {
            Entity.RemoveComponent(this);
        }
    }
}
