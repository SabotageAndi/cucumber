
using Io.Cucumber.Messages;

namespace Gherkin
{
    public class Token
    {
        public bool IsEOF { get { return Line == null; } }
        public IGherkinLine Line { get; set; }
        public TokenType MatchedType { get; set; }
        public string MatchedKeyword { get; set; }
        public string MatchedText { get; set; }
        public GherkinLineSpan[] MatchedItems { get; set; }
        public uint MatchedIndent { get; set; }
        public GherkinDialect MatchedGherkinDialect { get; set; }
        public Location Location { get; set; }

        public Token(IGherkinLine line, Location location)
        {
            Line = line;
            Location = location;
        }

        public void Detach()
        {
            if (Line != null)
                Line.Detach();
        }

        public string GetTokenValue()
        {
            return IsEOF ? "EOF" : Line.GetLineText(-1);
        }

        public override string ToString()
        {
            return string.Format("{0}: {1}/{2}", MatchedType, MatchedKeyword, MatchedText);
        }
    }
}