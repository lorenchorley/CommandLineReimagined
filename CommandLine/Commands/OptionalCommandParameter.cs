namespace Commands
{
    /// <summary>
    /// A parameter written as <c>-flag value</c> or <c>name: value</c> rather than
    /// positionally, and which the command can run without.
    /// </summary>
    public class OptionalCommandParameter : CommandParameter
    {
        public string Flag { get; set; } = string.Empty;

        public override bool IsOptional => true;

        /// <summary>Used when the user does not supply the argument at all.</summary>
        public Terminal.Execution.RuntimeValue Default { get; set; } = Terminal.Execution.RuntimeValue.Empty;
    }
}
