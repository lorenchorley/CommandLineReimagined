using System.Diagnostics.CodeAnalysis;
using System;
using CommandLineReimagine.Console;
using CommandLineReimagine.Console.Components;

public static class ComponentExtensions
{

    public static T? TryGetComponent<T>(this Component component) where T : Component
    {
        return component.Entity.TryGetComponent<T>();
    }

    public static bool TryGetComponent<T>(this Component component, [NotNullWhen(true)] out T? outComponent) where T : Component
    {
        return component.Entity.TryGetComponent<T>(out outComponent);
    }

    public static T GetComponent<T>(this Component component) where T : Component
    {
        return component.Entity.GetComponent<T>();
    }

    public static bool HasComponent<T>(this Component component) where T : Component
    {
        return component.Entity.HasComponent<T>();
    }

    public static void RemoveComponent<T>(this Component component) where T : Component
    {
        component.Entity.RemoveComponent<T>();
    }

    public static void RemoveComponent(this Component component, Component componentToRemove)
    {
        component.Entity.RemoveComponent(componentToRemove);
    }

    public static T AddComponent<T>(this Component component) where T : Component, new()
    {
        return component.Entity.AddComponent<T>();
    }

    public static T AddComponent<T>(this Component component, Action<T> configure) where T : Component, new()
    {
        return component.Entity.AddComponent<T>(configure);
    }

    public static T TryAddComponent<T>(this Component component) where T : Component, new()
    {
        return component.Entity.TryAddComponent<T>();
    }
}
