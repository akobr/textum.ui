﻿namespace BeaverSoft.Texo.Core.Inputting.History
{
    public interface IHistoryItem
    {
        Input Input { get; }

        HistoryItem Previous { get; }

        HistoryItem Next { get; }

        bool HasPrevious { get; }

        bool HasNext { get; }

        bool IsDeleted { get; }
    }
}