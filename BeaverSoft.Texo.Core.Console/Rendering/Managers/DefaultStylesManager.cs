using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace BeaverSoft.Texo.Core.Console.Rendering.Managers
{
    public class DefaultStylesManager : IConsoleStylesManager
    {
        private readonly ImmutableList<GraphicAttributes>.Builder styles;
        private readonly Dictionary<byte, Dictionary<uint, Dictionary<uint, byte>>> styleMap;

        private byte styleId;

        public DefaultStylesManager(GraphicAttributes defaultStyle)
        {
            styles = ImmutableList.CreateBuilder<GraphicAttributes>();
            styleMap = new Dictionary<byte, Dictionary<uint, Dictionary<uint, byte>>>();

            styles[0] = defaultStyle;
            styleId = 0;
        }

        public GraphicAttributes DefaultStyle => styles[0];

        public GraphicAttributes this[byte styleId] => styles[styleId];

        public byte GetOrCreateStyle(GraphicAttributes style)
        {
            byte atrributesKey = (byte)style.Style;
            if (!styleMap.TryGetValue(atrributesKey, out var backgroundMap))
            {
                styleMap[atrributesKey] = backgroundMap = new Dictionary<uint, Dictionary<uint, byte>>();
            }

            uint backgroundKey = MakeRgb(style.Background);
            if (!backgroundMap.TryGetValue(backgroundKey, out var foregorundMap))
            {
                backgroundMap[backgroundKey] = foregorundMap = new Dictionary<uint, byte>();
            }

            uint foregroundKey = MakeRgb(style.Foreground);
            if (!foregorundMap.TryGetValue(foregroundKey, out byte styleIndex))
            {
                styleIndex = GetNextStyleId();
                TryRemoveStyle(styleIndex);
                styles[styleIndex] = style;
                foregorundMap[foregroundKey] = styleIndex;
            }

            return styleIndex;
        }

        public IReadOnlyList<GraphicAttributes> ProvideStyles()
        {
            return styles.ToImmutable();
        }

        private void TryRemoveStyle(byte index)
        {
            if (index >= styles.Count)
            {
                return;
            }

            GraphicAttributes style = styles[index];
            styleMap[(byte)style.Style][MakeRgb(style.Background)].Remove(MakeRgb(style.Foreground));
        }

        private byte GetNextStyleId()
        {
            byte test = 255;
            ++test;

            return ++styleId == 0 ? ++styleId : styleId;
        }

        private static uint MakeRgb((byte R, byte G, byte B) color)
        {
            uint colorInt = (uint)(color.R << 16 | color.G << 8 | color.B << 0 | 255 << 24);
            string text = Convert.ToString(colorInt, 2);

            return colorInt;
        }
    }
}