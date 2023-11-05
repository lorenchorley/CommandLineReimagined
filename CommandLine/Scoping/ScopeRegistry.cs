using Terminal.Naming;

namespace Terminal.Scoping
{
    public class ScopeRegistry
    {
        public Scope Global { get; init; } = new Scope()
        {
            Namespace = new Namespace()
        };

    }
}
