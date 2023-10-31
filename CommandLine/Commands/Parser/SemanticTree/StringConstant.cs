using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record StringConstant : Constant
    {
        public int QuoteCount { get; private set; }
        public string QuoteString { get; private set; }

        private string _value;
        public string Value
        { 
            get
            {
                return _value;
            }
            set
            {
                (QuoteCount, _value) = DetectAndTrimMultipleDoubleQuotes(value);
                QuoteString = new string('"', QuoteCount);
            }
        }

        public override void Accept(ISemanticTreeVisitor visitor)
        {
            visitor.VisitStringConstant(this);
        }

        internal (int, string) DetectAndTrimMultipleDoubleQuotes(string value)
        {
            int count = 0;
            ReadOnlySpan<char> result = value;

            // TODO Tester
            var doubleQuote = new ReadOnlySpan<char>(new char[] { '"' });
            while (result.StartsWith(doubleQuote) && 
                   result.EndsWith(doubleQuote) &&
                   result.Length > 2 * count
                   )
            {
                count++;
                result = result.Slice(1, result.Length - 2);
            }

            return (count, result.ToString());
        }
    }
}
