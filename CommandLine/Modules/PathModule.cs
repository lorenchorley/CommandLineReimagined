using Terminal;
using Terminal.Naming;
using Terminal.Scoping;

namespace CommandLine.Modules
{
    public class PathModule
    {
        private readonly ScopeRegistry _scopeRegistry;

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

        public Scope CurrentPathScope
        {
            get
            {
                return new Scope()
                {
                    Namespace = new Namespace()
                    {
                        Segments = new string[] { "CurrentFolder" } // TODO
                    },
                    Parent = _scopeRegistry.Global
                };
            }
        }

        public PathModule(ScopeRegistry scopeRegistry)
        {
            _scopeRegistry = scopeRegistry;
        }

        /// <summary>
        /// A path as the user wrote it, made absolute against the current directory.
        /// </summary>
        /// <remarks>
        /// Every file command did this inline, each slightly differently. One place means
        /// they all agree on what a relative path is relative to.
        /// </remarks>
        public string Resolve(string path) =>
            Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(CurrentPath, path));

        public void Up()
        {
            CurrentPath = CurrentPath.GetFullPathOfOneDirectoryUp();
        }

        public bool Enter(string subdirectory)
        {
            // Normalised, so `cd ..` lands on the parent's real name rather than on a
            // path that ends in `..`.
            string newPath = Resolve(subdirectory);

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
