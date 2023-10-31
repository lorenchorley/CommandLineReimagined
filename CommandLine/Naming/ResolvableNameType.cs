namespace Terminal.Naming
{
    [Flags]
    public enum ResolvableNameType
    {
        None = 0,
        Command = 1 << 0,
        Type = 2 << 0,
        FileSystemObject = 3 << 0,
        Literal = 4 << 0,
        Variable = 5 << 0,
    }
}
