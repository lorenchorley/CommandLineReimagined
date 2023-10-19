using System.Collections.Generic;
using CommandLineReimagine.Commands.Parser.SemanticTree;

namespace CommandLineReimagine.Commands.Parser;

public class ParserResult : IParserResult<INode>
{
    public INode? Tree { get; set; }
    
    public List<string> Errors { get; internal set; } = new();

    public bool HasErrors
    {
        get
        {
            return Errors.Count > 0;
        }
    }
}
 