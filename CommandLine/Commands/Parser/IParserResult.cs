﻿using System.Collections.Generic;

namespace Commands.Parser;

    public interface IParserResult<TNode>
    {
        public TNode? Tree { get; }
        public List<string> Errors { get; }
        public bool HasErrors { get; }
    }
