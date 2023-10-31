using Commands.Parser;
using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Diagnostics;

namespace Terminal.Tests;

[TestClass]
public class ValidCommandTests
{
    [DataTestMethod]
    [DynamicData(nameof(GetValidCommandData), DynamicDataSourceType.Method)]
    public void ParseCommand(string command, string? _)
    {
        // Arrange
        CommandLineInterpreter lineInterpreter = new();
        var visitor = new SerialisationVisitor();


        // Act
        var result = lineInterpreter.Parse(command);


        // Assert
        result.Switch(
            tree =>
            {

                tree.Accept(visitor);
                Debug.WriteLine(visitor.GetResult());
            },
            errors => Assert.Fail(string.Join("\n", errors))
            );
    }

    [DataTestMethod]
    [DynamicData(nameof(GetValidCommandData), DynamicDataSourceType.Method)]
    public void ReserialiseCommand(string command, string? expectedReserialisation)
    {
        // Arrange
        CommandLineInterpreter lineInterpreter = new();
        var visitor = new SerialisationVisitor();
        var visitorWithIndentation = new SerialisationVisitor()
        {
            UseIdentation = true,
            UseNewLineOnPipe = true
        };


        // Act
        ParserResult result1 = lineInterpreter.Parse(command);

        string reserialisatedCommand =
            result1.Match(
                tree =>
                {
                    tree.Accept(visitor);
                    return visitor.GetResult();
                },
                errors => throw new Exception(string.Join("\n", errors))
                );

        ParserResult result2 = lineInterpreter.Parse(command);


        // Assert
        Assert.AreEqual(expectedReserialisation ?? command, reserialisatedCommand, $"\nSimilarity : {FindSimilarity(command, reserialisatedCommand)}%");
        Assert.AreEqual(result1.ToString(), result2.ToString(), $"\nSimilarity : {FindSimilarity(result1.ToString(), result2.ToString())}%");

        result1.AsT0.Accept(visitorWithIndentation);
        Debug.WriteLine(visitorWithIndentation.GetResult());
    }

    private static IEnumerable<object[]> GetValidCommandData()
    {
        yield return new object[] { @"", null };
        yield return new object[] { @"<objInstance/>", null };
        yield return new object[] { @"<objInstance></objInstance>", "<objInstance/>" };
        yield return new object[] { @"<objInstance><child/></objInstance>", null };
        yield return new object[] { @"<objInstance>[prop=""""str""""]</objInstance>", null };
        yield return new object[] { @"<objInstance attr=value/>", null };
        yield return new object[] { @"<variableName|objInstance></>", @"<variableName|objInstance/>" };
        yield return new object[] { @"command", null };
        yield return new object[] { @"command requiredParameter", null };
        yield return new object[] { @"command -with optionalParameter", null };
        yield return new object[] { @"command()", null };
        yield return new object[] { @"command(requiredParameter)", null };
        yield return new object[] { @"command(with: optionalParameter)", null };
        yield return new object[] { @"command() | anotherCommand()", null };
        yield return new object[] { @"<value/> | anotherCommand()", null };

        yield return new object[] { @"command | <objInstance><child/><child/><child val=$v/><child><child/></child></objInstance>", null };
    }


    /// <summary>
    /// Implémentation du "Edit distance algorithm" pour calculer la distance ou similarité entre deux chaînes de caractères.
    /// </summary>
    /// <param name="X"></param>
    /// <param name="Y"></param>
    /// <returns></returns>
    public static int GetEditDistance(string X, string Y)
    {
        int m = X.Length;
        int n = Y.Length;

        int[][] T = new int[m + 1][];
        for (int i = 0; i < m + 1; ++i)
        {
            T[i] = new int[n + 1];
        }

        for (int i = 1; i <= m; i++)
        {
            T[i][0] = i;
        }
        for (int j = 1; j <= n; j++)
        {
            T[0][j] = j;
        }

        int cost;
        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                cost = X[i - 1] == Y[j - 1] ? 0 : 1;
                T[i][j] = Math.Min(Math.Min(T[i - 1][j] + 1, T[i][j - 1] + 1),
                        T[i - 1][j - 1] + cost);
            }
        }

        return T[m][n];
    }

    public static int FindSimilarity(string x, string y)
    {
        if (x == null || y == null)
        {
            throw new ArgumentException("Strings must not be null");
        }

        double maxLength = Math.Max(x.Length, y.Length);
        if (maxLength > 0)
        {
            // optionally ignore case if needed
            double ratio = (maxLength - GetEditDistance(x, y)) / maxLength;
            return (int)(ratio * 100);
        }
        return 100;
    }
}