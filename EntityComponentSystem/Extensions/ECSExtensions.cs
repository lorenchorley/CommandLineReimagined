using EntityComponentSystem;
using System.Runtime.InteropServices;

public static class ECSExtensions
{
    public static List<T> ComponentTreeSearchLowestFirst<T>(this ECS ecs) where T : Component
    {
        List<T> matches = new();
        InternalComponentTreeSearchLowestFirst<T>(ecs.RootEntity.Children, matches);
        return matches;
    }

    private static void InternalComponentTreeSearchLowestFirst<T>(Span<Entity> entities, List<T> matches) where T : Component
    {
        foreach (var entity in entities)
        {
            var children = entity.Children;
            InternalComponentTreeSearchLowestFirst(children, matches);

            if (entity.TryGetComponent(out T? match))
            {
                matches.Add(match);
            }
        }
    }
}
