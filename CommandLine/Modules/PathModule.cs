using System.IO;

namespace CommandLine.Modules
{
    public class PathModule
    {
        public string CurrentPath { get; private set; } = @"C:\CLR\"; //Environment.CurrentDirectory;
        public string CurrentFolder
        {
            get
            {
                if (string.Equals(Path.GetPathRoot(CurrentPath), CurrentPath)) // Racine
                {
                    return Path.TrimEndingDirectorySeparator(CurrentPath);
                }
                else
                {
                    return CurrentPath.GetLowestDirectory();
                }
            }
        }

        public void Up()
        {
            CurrentPath = CurrentPath.GetFullPathOfOneDirectoryUp();
        }

        public bool Enter(string subdirectory)
        {
            string newPath = Path.Combine(CurrentPath, subdirectory);

            if (!Directory.Exists(newPath))
            {
                return false;
            }

            CurrentPath = newPath;

            return true;
        }

        public bool MoveTo(string path)
        {
            string newPath = path;

            if (!Directory.Exists(newPath))
            {
                return false;
            }

            CurrentPath = newPath;

            return true;
        }

        public bool IsCurrentPathTheRoot()
        {
            return string.Equals(CurrentPath, CurrentPath.GetFullPathOfOneDirectoryUp(), System.StringComparison.Ordinal);
        }
    }
}
