﻿using System.Collections.Generic;
using BeaverSoft.Texo.Commands.NugetManager.Model;

namespace BeaverSoft.Texo.Commands.NugetManager.Processing
{
    public interface IProcessingStrategy<TResult>
    {
        TResult Process(string filePath);
    }

    public interface IConfigProcessingStrategy : IProcessingStrategy<IConfig>
    {
        // no member
    }

    public interface IProjectProcessingStrategy : IProcessingStrategy<IProject>
    {
        // no member
    }

    public interface ISolutionProcessingStrategy : IProcessingStrategy<IEnumerable<string>>
    {
        // no member
    }
}
