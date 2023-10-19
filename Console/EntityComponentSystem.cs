using CommandLineReimagined.Console.Entities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CommandLineReimagine.Console.Components;
using CommandLineReimagine.Console.Interaction;

namespace CommandLineReimagine.Console
{
    public class EntityComponentSystem
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
