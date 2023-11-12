using EntityComponentSystem.Serialisation;
using Newtonsoft.Json;

namespace EntityComponentSystem
{
    public abstract class ActiveComponent : Component
    {
        public abstract void OnUpdateState();
        //public abstract void OnDraw();
    }
}
