using System.Threading.Tasks;
using BeaverSoft.Texo.Commands.FileManager;
using BeaverSoft.Texo.Commands.Functions;
using BeaverSoft.Texo.Commands.NugetManager;
using BeaverSoft.Texo.Core;
using BeaverSoft.Texo.Core.Configuration;
using BeaverSoft.Text.Client.VisualStudio.Core.Environment;
using BeaverSoft.Text.Client.VisualStudio.Core.Providers;
using Commands.Calc;
using Commands.CodeBaseSearch;
using Commands.CommandLine;
using Commands.ReferenceCheck;
using Commands.SpinSport;
using StrongBeaver.Core.Container;

namespace BeaverSoft.Text.Client.VisualStudio.Startup
{
    public static class TexoEngineConfig
    {
        public static Task InitialiseWithCommandsAsync(this TexoEngine engine, SimpleIoc container)
        {
            TexoConfiguration.Builder configuration = TexoConfiguration.CreateDefault().ToBuilder();

            string solutionFile = container.GetInstance<ISolutionPathProvider>().GetPath();
            if (!string.IsNullOrEmpty(solutionFile))
            {
                configuration.Environment.Variables.Add(VsVariableNames.SOLUTION_FILE, solutionFile);
            }

            configuration.Runtime.Commands.AddRange(new[] {
                ReferenceCheckCommand.BuildConfiguration(),
                CommandLineCommand.BuildConfiguration(),
                FileManagerBuilder.BuildCommand(),
                NugetManagerBuilder.BuildCommand(),
                CalcCommand.BuildConfiguration(),
                //ClipboardBuilder.BuildCommand(),
                FunctionsBuilder.BuildCommand(),
                CodeBaseSearchBuilder.BuildCommand(),
                SpinSportBuilder.BuildCommand()
            });

            return engine.InitialiseAsync(configuration.ToImmutable());
        }
    }
}
