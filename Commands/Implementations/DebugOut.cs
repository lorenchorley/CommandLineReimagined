using CommandLine.Modules;
using EntityComponentSystem;
using System.Diagnostics;

namespace Commands.Implementations
{
    public class DebugOut : CommandActionSync
    {
        private readonly ECS _ecs;

        public override CommandDefinition Profile { get; } =
            new CommandDefinition(
                Name: "debug",
                Description: "",
                KeyWords: "diagnostic",
                Parameters: new CommandParameter[]
                {
                },
                CommandActionType: typeof(DebugOut)
            );

        public DebugOut(ECS ecs)
        {
            _ecs = ecs;
        }

        public override void Invoke(CommandParameterValue[] args, CliBlock scope)
        {
            // Convertir toutes les entites en chaine
            //string text = SerialiseIntoXML();
            //string text = SerialiseIntoJSON();
            string text = SerialiseCustom();

            // Ecrire le JSON dans un fichier
            string fileName = Path.GetFullPath("debug.out");
            File.WriteAllText(fileName, text);

            // Ouvrir l'editeur de texte par défaut avec ce fichier
            OpenWithDefaultProgram(fileName);
        }

        private string SerialiseCustom()
        {
            EntitySerializer s = new();
            return _ecs.AccessEntities(list => s.SerializeEntities(list));
        }

        //private string SerialiseIntoXML()
        //{
        //    XmlSerializer serializer = new XmlSerializer(typeof(Entity), new Type[] { typeof(Component) });
        //    using StringWriter writer = new StringWriter();
        //    serializer.Serialize(writer, _ecs.RegisteredEntities);

        //    // Get the XML string
        //    return writer.ToString();
        //}

        //private string SerialiseIntoJSON()
        //{
        //    JsonSerializerSettings settings = new JsonSerializerSettings()
        //    {
        //        TypeNameHandling = TypeNameHandling.All,
        //        Converters = new List<JsonConverter>()
        //        {
        //            new RectangleFConverter()
        //        }
        //    };
        //    var text = JsonConvert.SerializeObject(_ecs.RegisteredEntities, Formatting.Indented, settings);
        //    return text;
        //}

        public static void OpenWithDefaultProgram(string path)
        {
            using Process fileopener = new Process();

            fileopener.StartInfo.FileName = "explorer";
            fileopener.StartInfo.Arguments = "\"" + path + "\"";
            fileopener.Start();
        }

        public override void InvokeUndo(CommandParameterValue[] args, CliBlock scope)
        {

        }
    }
}
