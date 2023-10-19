using System.IO;
using System.Linq;

public static class PathExtensions
{
    public static string GetLowestDirectory(this string path)
    {
        return Path.TrimEndingDirectorySeparator(path).Split('\\').Last();
    }
    public static string GetFullPathOfOneDirectoryUp(this string path)
    {
        return Path.GetFullPath(Path.Combine(path, ".."));
    }

}
