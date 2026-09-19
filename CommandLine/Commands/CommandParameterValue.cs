using Terminal.Execution;

namespace Commands
{
    /// <summary>
    /// A declared parameter bound to the value the user actually supplied.
    /// </summary>
    public class CommandParameterValue
    {
        public CommandParameter? Parameter { get; set; }

        /// <summary>
        /// The bound value. Previously a raw string, which meant only quoted string
        /// literals could ever be bound; it is a <see cref="RuntimeValue"/> now so
        /// identifiers, numbers, variable references and piped results all work.
        /// </summary>
        public RuntimeValue Value { get; set; } = RuntimeValue.Empty;

        /// <summary>The value as text, which is what most commands want.</summary>
        public string Text => Value.ToArgumentString();
    }
}
