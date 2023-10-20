using System.Collections.Generic;
using CommandLineReimagined.Commands.Parser.SemanticTree;

namespace CommandLineReimagined.Commands.Parser;

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
 