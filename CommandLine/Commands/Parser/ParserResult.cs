using System.Collections.Generic;
using Commands.Parser.SemanticTree;

namespace Commands.Parser;

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
 