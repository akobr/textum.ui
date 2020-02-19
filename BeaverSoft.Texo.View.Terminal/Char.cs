namespace BeaverSoft.Texo.View.Terminal
{
    /// <summary>
    /// Char and style
    /// </summary>
    public struct Char
    {
        /// <summary>
        /// Unicode character
        /// </summary>
        public char c;

        /// <summary>
        /// Main style ID of the character
        /// </summary>
        public byte mainStyleId;

        /// <summary>
        /// Secondary styles bit mask
        /// </summary>
        /// <remarks>Bit 1 in position n means that this char will rendering by FastColoredTextBox.Styles[n]</remarks>
        public StyleIndex style;

        public Char(char c)
        {
            this.c = c;
            mainStyleId = TextStylesManager.DEFAULT_STYLE_ID;
            style = StyleIndex.None;
        }
    }
}