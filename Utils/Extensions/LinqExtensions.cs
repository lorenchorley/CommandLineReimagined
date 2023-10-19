using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

// TODO Mettre dans un projet "Common" en dehors des projets temporaires

/// <summary>
/// Cette classe est originale
/// </summary>
public static class LinqExtensions
{
    /// <summary>
    /// Permet de concatener une séries de chaine dans la façon LINQ pour permettre une syntaxe plus fluide
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <returns></returns>
    [DebuggerNonUserCode]
    public static string Join(this IEnumerable<string> source, char separator)
    {
        return string.Join(separator, source);
    }
    
    /// <summary>
    /// Permet de concatener une séries de chaine dans la façon LINQ pour permettre une syntaxe plus fluide
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <returns></returns>
    [DebuggerNonUserCode]
    public static string Join(this IEnumerable<string> source, string? separator)
    {
        return string.Join(separator, source);
    }

    /// <summary>
    /// Permet de concatener une séries de chaine dans la façon LINQ pour permettre une syntaxe plus fluide
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <returns></returns>
    [DebuggerNonUserCode]
    public static string Join<T>(this IEnumerable<T> source, char separator)
    {
        return string.Join(separator, source);
    }
    
    /// <summary>
    /// Permet de concatener une séries de chaine dans la façon LINQ pour permettre une syntaxe plus fluide
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <returns></returns>
    [DebuggerNonUserCode]
    public static string Join<T>(this IEnumerable<T> source, string? separator)
    {
        return string.Join(separator, source);
    }

    ///// <summary>
    ///// Permet de éliminer les valeur de None dans la flux
    ///// </summary>
    ///// <typeparam name="T"></typeparam>
    ///// <param name="source"></param>
    ///// <returns></returns>
    //[DebuggerNonUserCode]
    //public static IEnumerable<T> SelectWithValue<T>(this IEnumerable<Option<T>> source)
    //{
    //    return List.choose(source, x => x);
    //}

    /// <summary>
    /// Permet 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <returns></returns>
    [DebuggerNonUserCode]
    public static IEnumerable<T> MergeSubSequences<T>(this IEnumerable<T[]> sequenceOfArrays)
    {
        return sequenceOfArrays.SelectMany(x => x);
    }

    /// <summary>
    /// Comme Take de Linq, mais si count est à null, on ne fait rien
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="count"></param>
    /// <returns></returns>
    [DebuggerNonUserCode]
    public static IEnumerable<T> OptionallyTake<T>(this IEnumerable<T> source, int? count)
    {
        if (count.HasValue)
        {
            return source.Take(count.Value);
        }
        else
        {
            return source;
        }
    }

    /// <summary>
    /// Permet filtrer la source par liste d'élement.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="filter">La liste d'élement à garder</param>
    /// <param name="selector">Selector qui permet de mettre en correspondance la liste à garder et la séquence</param>
    /// <returns></returns>
    [DebuggerNonUserCode]
    public static IEnumerable<T> OptionallySelect<T, S>(this IEnumerable<T> source, S[]? filter, Func<T, S> selector)
    {
        if (filter != null)
        {
            System.Collections.Generic.HashSet<S> filteredElements = new(filter);
            return source.Where(t => filter.Contains(selector(t)));
        }
        else
        {
            return source;
        }
    }

    /// <summary>
    /// Permet filtrer la source par liste d'élement.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="condition">La condition à utiliser</param>
    /// <param name="predicate">Predicat qui permet de filtrer la séquence si la condition est vraie</param>
    /// <returns></returns>
    [DebuggerNonUserCode]
    public static IEnumerable<T> OptionallyWhere<T>(this IEnumerable<T> source, bool condition, Func<T, bool> predicate)
    {
        if (condition)
        {
            return source.Where(predicate);
        }
        else
        {
            return source;
        }
    }
    
    ///// <summary>
    ///// Comme Append mais conditionnel sur la condition y passée
    ///// </summary>
    ///// <typeparam name="T"></typeparam>
    ///// <param name="source"></param>
    ///// <param name="rhs"></param>
    ///// <param name="condition"></param>
    ///// <returns></returns>
    //[DebuggerNonUserCode]
    //public static IEnumerable<T> AppendIf<T>(this IEnumerable<T> source, IEnumerable<T> rhs, bool condition)
    //{
    //    if (condition)
    //    {
    //        return source.Append(rhs);
    //    }
    //    else
    //    {
    //        return source;
    //    }
    //}

    ///// <summary>
    ///// Comme Append mais conditionnel sur le prédicat y passé
    ///// </summary>
    ///// <typeparam name="T"></typeparam>
    ///// <param name="source"></param>
    ///// <param name="rhs"></param>
    ///// <param name="predicate"></param>
    ///// <returns></returns>
    //[DebuggerNonUserCode]
    //public static IEnumerable<T> AppendIf<T>(this IEnumerable<T> source, IEnumerable<T> rhs, Func<bool> predicate)
    //{
    //    if (predicate())
    //    {
    //        return source.Append(rhs);
    //    }
    //    else
    //    {
    //        return source;
    //    }
    //}

    /// <summary>
    /// Comme First de Linq mais avec une exception personnalisée
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="exceptionFactory"></param>
    /// <returns></returns>
    [DebuggerNonUserCode]
    public static T FirstOrException<T>(this IEnumerable<T> source, Func<Exception> exceptionFactory)
    {
        var first = source.FirstOrDefault();

        if (first == null)
        {
            throw exceptionFactory.Invoke();
        }

        return first;
    }

    /// <summary>
    /// Permet d'itérater un IEnumerable
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    [DebuggerNonUserCode]
    public static IEnumerable<T> ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        foreach (T item in source)
        {
            action(item);
        }

        return source;
    }

    /// <summary>
    /// Permet de faire une conversion et en même temps un filtrage.
    /// Autrement dit il est equivalent de faire Select(..) puis Where(r => r != null).
    /// </summary>
    /// <typeparam name="TSource"></typeparam>
    /// <typeparam name="TResult"></typeparam>
    /// <param name="source"></param>
    /// <param name="selector"></param>
    /// <returns></returns>
    [DebuggerNonUserCode]
    public static IEnumerable<TResult> Choose<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult?> selector) /*where TResult : class*/
    {
        if (selector == null)
        {
            throw new ArgumentNullException(nameof(selector));
        }

        if (source != null)
        {
            foreach (TSource item in source)
            {
                TResult? value = selector(item);
                if (value != null)
                {
                    yield return value;
                }
            }
        }
    }
    
    /// <summary>
    /// Permet de filtrer toutes les valeurs null dans une séquence.
    /// Autrement dit il est equivalent de faire Select(..) puis Where(r => r != null).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <param name="selector"></param>
    /// <returns></returns>
    [DebuggerNonUserCode]
    public static IEnumerable<T> FilterNull<T>(this IEnumerable<T?> source)
    {
        return source.Choose(x => x);
    }

    ///// <summary>
    ///// Equivalent de faire Select(..) puis Where(r => r != null)
    ///// </summary>
    ///// <typeparam name="TSource"></typeparam>
    ///// <typeparam name="TResult"></typeparam>
    ///// <param name="source"></param>
    ///// <param name="selector"></param>
    ///// <returns></returns>
    //public static IEnumerable<TResult> Choose<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, Option<TResult>> selector) /*where TResult : class*/
    //{
    //    if (selector == null)
    //    {
    //        throw new ArgumentNullException(nameof(selector));
    //    }

    //    if (source != null)
    //    {
    //        foreach (TSource item in source)
    //        {
    //            Option<TResult> value = selector(item);
    //            if (value.IsSome)
    //            {
    //                var enumerator = value.GetEnumerator();
    //                enumerator.MoveNext();
    //                yield return enumerator.Current;
    //            }
    //        }
    //    }
    //}

    ///// <summary>
    ///// Equivalent de faire Select(..) puis Where(r => r != null)
    ///// </summary>
    ///// <typeparam name="TSource"></typeparam>
    ///// <typeparam name="TResult"></typeparam>
    ///// <param name="source"></param>
    ///// <param name="selector"></param>
    ///// <returns></returns>
    //public static IEnumerable<TResult> ChooseS<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult?> selector) where TResult : struct
    //{
    //    if (selector == null)
    //    {
    //        throw new ArgumentNullException(nameof(selector));
    //    }

    //    if (source != null)
    //    {
    //        foreach (TSource item in source)
    //        {
    //            TResult? value = selector(item);
    //            if (value.HasValue)
    //            {
    //                yield return value.Value;
    //            }
    //        }
    //    }
    //}
}
