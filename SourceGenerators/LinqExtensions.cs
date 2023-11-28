using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

/// <summary>
/// Cette classe est originale
/// </summary>
internal static class LinqExtensions
{
    /// <summary>
    /// Permet de concatener une séries de chaine dans la façon LINQ pour permettre une syntaxe plus fluide
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="source"></param>
    /// <returns></returns>
    [DebuggerNonUserCode]
    internal static string Join(this IEnumerable<string> source, string? separator)
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
    internal static string Join<T>(this IEnumerable<T> source, string? separator)
    {
        return string.Join(separator, source);
    }

}
