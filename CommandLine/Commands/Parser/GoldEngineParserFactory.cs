using GOLD;
using System;
using System.IO;
using System.Reflection;

namespace CommandLineReimagine.Commands.Parser;

public class GoldEngineParserFactory
{
    public static GOLD.Parser BuildParser(string embeddedRessource)
    {
        //On crée le parser
        GOLD.Parser parser = new();

        //Inutile de garder les réductions de type <A> ::= <B>
        parser.TrimReductions = true;

        // On charge le fichier Embedded Ressource provenant de l'assembly appellant cette méthode.
        Assembly assembly = Assembly.GetCallingAssembly();
        //var list = assembly.GetManifestResourceNames();
        using Stream? stream = assembly.GetManifestResourceStream(embeddedRessource);

        if (stream == null)
            throw new Exception("Le fichier grammaire du parser de filtre n'a pas été trouvé");

        using BinaryReader reader = new BinaryReader(stream);

        parser.LoadTables(reader);

        return parser;
    }
}
