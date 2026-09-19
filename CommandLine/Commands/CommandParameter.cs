namespace Commands
{
    public class CommandParameter
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Whether the command can run without this argument. Required parameters that
        /// are missing are reported as an error rather than silently binding nothing.
        /// </summary>
        public virtual bool IsOptional => false;

        /// <summary>
        /// Whether the parameter may be satisfied by the value piped in, when the user
        /// did not write it out. Lets <c>ls | echo</c> work without special-casing echo.
        /// </summary>
        public bool AcceptsPipedInput { get; set; }

        /// <summary>
        /// A parameter whose absence is not an error, and whose value defaults when the
        /// user omits it.
        /// </summary>
        public static OptionalCommandParameter Optional(string name, string description = "", string? flag = null)
            => new() { Name = name, Description = description, Flag = flag ?? name };
    }
}
