using System.Collections.Generic;

namespace BeaverSoft.Texo.Core.Console.Rendering
{
    public interface IViewStyles
    {
        GraphicAttributes this[byte styleId] { get; }

        GraphicAttributes DefaultStyle { get; }

        byte GetOrCreateStyle(GraphicAttributes style);

        IReadOnlyList<GraphicAttributes> ProvideStyles();
    }
}