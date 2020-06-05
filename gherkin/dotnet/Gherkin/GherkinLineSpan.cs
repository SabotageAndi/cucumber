namespace Gherkin
{
    public struct GherkinLineSpan
    {
        /// <summary>
        /// One-based line position
        /// </summary>
        public uint Column { get; private set; }

        /// <summary>
        /// Text part of the line
        /// </summary>
        public string Text { get; private set; }

        public GherkinLineSpan(uint column, string text) : this()
        {
            Column = column;
            Text = text;
        }
    }
}
