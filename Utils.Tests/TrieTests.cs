using Utils.Search.Trie;

namespace Utils.Tests;

[TestClass]
public class TrieTests
{
    [TestMethod]
    public void TrieSearchTest()
    {
        // Arrange
        string[] words = new string[] { "hello", "world", "help", "me", "please" };
        TrieSearch trie = new TrieSearch(words);

        // Act
        var result = trie.Search("he");

        // Assert
        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.Contains("hello"));
        Assert.IsTrue(result.Contains("help"));
    }
}