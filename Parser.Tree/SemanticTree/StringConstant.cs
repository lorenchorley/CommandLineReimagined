using Isagri.Reporting.Quid.RequestFilters.SemanticTree;
using System.Collections.Generic;

namespace Commands.Parser.SemanticTree
{
    public record StringConstant : Constant
    {
        public int QuoteCount { get; private set; }
        public string QuoteString { get; private set; } = null!;

        private string _value = null!;
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

        /// <summary>A string whose delimiter is already known.</summary>
        /// <remarks>
        /// The combinator grammar matches the delimiter itself, longest first, so it knows
        /// how many quotes opened the string and what lies between them. Handing the
        /// setter the whole literal instead made it guess again from the outside in, and
        /// the guess stopped early on a short body: <c>""""</c> came out as the text
        /// <c>""</c> and <c>"""a"""</c> as <c>"a"</c>, where every delimiter is meant to
        /// carry the same value. The setter stays for the GOLD interpreter.
        /// </remarks>
        public static StringConstant Delimited(int quoteCount, string body)
        {
            var constant = new StringConstant();
            constant.QuoteCount = quoteCount;
            constant.QuoteString = new string('"', quoteCount);
            constant._value = body;
            return constant;
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
