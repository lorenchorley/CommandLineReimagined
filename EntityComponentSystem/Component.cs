using EntityComponentSystem;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace EntityComponentSystem
{
    public abstract class Component
    {
        public int Id { get; init; }

        private Entity? _entity;
        [JsonIgnore]
        public Entity Entity
        {
            get
            {
                ArgumentNullException.ThrowIfNull(_entity);

                return _entity;
            }
            set
            {
                // On ne peut pas changer l'entité d'un composant déjà initialisé
                if (value != null && _entity != null)
                {
                    throw new InvalidOperationException("Entity is already set.");
                }

                _entity = value;

                // Si on n'est pas en train de supprimer l'entité
                if (value != null)
                {
                    // On a besoin de s'assurer que les dépendances sont satisfaites
                    InsureDependencies();
                }
            }
        }

        [JsonIgnore]
        public ECS ECS => Entity.ECS;
        
        public abstract IEnumerable<(string, string)> SerialisableDebugProperties { get; }

        protected virtual void InsureDependencies() { }

        internal void OnDestroy()
        {
            // TODO ?
        }
    }
}
