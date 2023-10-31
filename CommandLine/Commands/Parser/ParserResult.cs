using System.Collections.Generic;
using Commands.Parser.SemanticTree;
using OneOf;

namespace Commands.Parser;

[GenerateOneOf]
public partial class ParserResult : OneOfBase<RootNode, List<string>>
{

}


