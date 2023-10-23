using GOLD;
using Commands.Parser.SemanticTree;

namespace Commands.Parser;

public static class GoldEngineExtensions
{
    /// <summary>
    /// Faire le passe-plat de la première valeur de la réduction
    /// </summary>
    /// <param name="reduction"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    public static INode PassOn(this Reduction reduction, int index = 0)
    {
        return (INode)reduction[index].Data;
    }
}
