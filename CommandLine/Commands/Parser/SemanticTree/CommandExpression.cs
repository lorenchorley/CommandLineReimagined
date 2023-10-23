﻿using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record CommandExpression : INode
    {
        public CommandName Id { get; init; }
        public CommandArguments Arguments { get; init; }
    }
}
