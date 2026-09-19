namespace Terminal.Execution;

/// <summary>
/// A value produced by an executed command.
/// </summary>
/// <remarks>
/// Distinct from <see cref="Commands.Parser.SemanticTree.Value"/>, which is what the
/// grammar produced. That is syntax; this is the result of running something. Pipes
/// thread one of these from each command to the next, which is why commands return a
/// value instead of writing straight to the console.
/// </remarks>
public abstract record RuntimeValue
{
    public static readonly RuntimeValue Empty = new EmptyValue();

    /// <summary>How the value reads when a human has to see it.</summary>
    public abstract string ToDisplayString();

    /// <summary>
    /// The value as a command argument would want it. Commands take paths and names as
    /// text, so every value has to be able to answer this.
    /// </summary>
    public virtual string ToArgumentString() => ToDisplayString();
}

public sealed record EmptyValue : RuntimeValue
{
    public override string ToDisplayString() => string.Empty;
}

public sealed record TextValue(string Text) : RuntimeValue
{
    public override string ToDisplayString() => Text;
}

public sealed record NumberValue(double Number) : RuntimeValue
{
    public override string ToDisplayString() =>
        Number.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record BooleanValue(bool Boolean) : RuntimeValue
{
    public override string ToDisplayString() => Boolean ? "true" : "false";
}

public enum PathKind
{
    File,
    Directory,

    /// <summary>The parent directory entry, which navigates rather than names a target.</summary>
    Parent,
}

public sealed record PathValue(string Path, PathKind Kind) : RuntimeValue
{
    public string Name => Kind == PathKind.Parent
        ? "up"
        : System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(Path));

    public override string ToDisplayString() => Kind == PathKind.Directory ? Name + "\\" : Name;

    // Piping a path into another command should hand over the full path, not the label.
    public override string ToArgumentString() => Path;
}

public sealed record ListValue(IReadOnlyList<RuntimeValue> Items) : RuntimeValue
{
    public override string ToDisplayString() => string.Join(" ", Items.Select(i => i.ToDisplayString()));
}

/// <summary>
/// The result of an object instance tag, e.g. <c>&lt;thing size=3/&gt;</c>.
/// </summary>
public sealed record ObjectValue(
    string TypeName,
    IReadOnlyDictionary<string, RuntimeValue> Attributes,
    IReadOnlyList<ObjectValue> Children) : RuntimeValue
{
    public override string ToDisplayString()
    {
        var attributes = Attributes.Count == 0
            ? string.Empty
            : " " + string.Join(" ", Attributes.Select(a => $"{a.Key}={a.Value.ToDisplayString()}"));

        return Children.Count == 0
            ? $"<{TypeName}{attributes}/>"
            : $"<{TypeName}{attributes}>{string.Concat(Children.Select(c => c.ToDisplayString()))}</{TypeName}>";
    }
}
