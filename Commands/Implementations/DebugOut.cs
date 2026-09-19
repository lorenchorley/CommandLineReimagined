using EntityComponentSystem;
using EntityComponentSystem.Serialisation;
using System.Diagnostics;
using Terminal.Execution;

namespace Commands.Implementations
{
    public class DebugOut : CommandActionSync
    {
        private readonly ECS _ecs;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "debug",
                Description: "Write the entity/component tree to a file",
                KeyWords: "diagnostic dump inspect",
                Parameters: new CommandParameter[]
                {
                    CommandParameter.Optional("open", "Open the file in the default editor"),
                },
                CommandActionType: typeof(DebugOut)
            );

        public DebugOut(ECS ecs)
        {
            _ecs = ecs;
        }

        public override RuntimeValue Invoke(CommandInvocation invocation)
        {
            // Was `throw new NotImplementedException()` behind a commented-out serialiser.
            // The ECS already has one, so use it rather than leaving the command dead.
            string text = new EventSourceSerialiser().SerialiseEntityComponentTree(_ecs.RootEntity);

            string fileName = Path.GetFullPath("debug.out");
            File.WriteAllText(fileName, text);

            if (invocation.TryValue("open", out var open) && open is BooleanValue { Boolean: true })
            {
                OpenWithDefaultProgram(fileName);
            }

            return new PathValue(fileName, PathKind.File);
        }

        public override void InvokeUndo(CommandInvocation invocation)
        {
        }

        public static void OpenWithDefaultProgram(string path)
        {
            using Process fileopener = new Process();
            fileopener.StartInfo.FileName = "explorer";
            fileopener.StartInfo.Arguments = "\"" + path + "\"";
            fileopener.Start();
        }
    }
}
