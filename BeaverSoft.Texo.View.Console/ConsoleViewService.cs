using System.Collections.Immutable;
using System.Threading;
using BeaverSoft.Texo.Core.Configuration;
using BeaverSoft.Texo.Core.Environment;
using BeaverSoft.Texo.Core.Inputting;
using BeaverSoft.Texo.Core.Runtime;
using BeaverSoft.Texo.Core.View;
using StrongBeaver.Core.Messaging;

using SysConsole = System.Console;

namespace BeaverSoft.Texo.View.Console
{
    public class ConsoleViewService : IViewService, IPromptableViewService
    {
        private const string TITLE_TEXO = "Texo UI";

        private readonly IConsoleRenderService renderer;
        private readonly CursorPosition position;

        private IExecutor executor;
        private TexoConfiguration configuration;
        private string workingDir;
        private string prompt;
        private bool disposed;

        public ConsoleViewService(IConsoleRenderService renderer)
        {
            this.renderer = renderer;
            position = new CursorPosition();
        }

        public void Initialise(IExecutor trigger)
        {
            executor = trigger;
        }

        public void Start()
        {
            SysConsole.Title = TITLE_TEXO;
            StartInput();
        }

        private void StartInput()
        {
            while (!disposed)
            {
                PreparePrompt();
                WaitForInput();
            }
        }

        private async void WaitForInput()
        {
            string input = SysConsole.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                return;
            }

            using (new ConsoleStopwatch())
            {
                await executor.ProcessAsync(input, CancellationToken.None);
            }
        }

        public void Render(Input input, IImmutableList<IItem> items)
        {
            foreach (IItem item in items)
            {
                renderer.Write(item);
            }
        }

        public void RenderIntellisense(Input input, IImmutableList<IItem> items)
        {
            // TODO
        }

        public void RenderProgress(IProgress progress)
        {
            // TODO
        }

        public void Update(string key, IItem item)
        {
            // TODO
        }

        public string GetNewInput()
        {
            return SysConsole.ReadLine();
        }

        public void SetInput(string input)
        {
            // TODO: overwrite
            SysConsole.Write(input);
        }

        public void AddInput(string append)
        {
            SysConsole.Write(" " + append.Trim());
        }

        public void ShowProgress(int id, string name, int progress)
        {
            SysConsole.WriteLine($"{name}: {progress}%");
        }

        public void Dispose()
        {
            disposed = true;

            SysConsole.WriteLine();
            SysConsole.WriteLine("Texo console application exit.");
        }

        private void PreparePrompt()
        {
            SysConsole.WriteLine();
            WritePrompt();

            position.Top = SysConsole.CursorTop;
            position.Left = SysConsole.CursorLeft;
        }

        private void WritePrompt()
        {
            if (configuration.Ui.ShowWorkingPathAsPrompt)
            {
                TexoConsole.WritePrompt(workingDir);
            }
            else
            {
                TexoConsole.WritePrompt(prompt);
            }
        }

        void IMessageBusRecipient<ISettingUpdatedMessage>.ProcessMessage(ISettingUpdatedMessage message)
        {
            configuration = message.Configuration;
        }

        void IMessageBusRecipient<IVariableUpdatedMessage>.ProcessMessage(IVariableUpdatedMessage message)
        {
            if (message.Name != VariableNames.CURRENT_DIRECTORY)
            {
                return;
            }

            workingDir = message.NewValue;
            prompt = configuration.Ui.Prompt;
        }

        void IMessageBusRecipient<IClearViewOutputMessage>.ProcessMessage(IClearViewOutputMessage message)
        {
            SysConsole.Clear();
        }

        void IMessageBusRecipient<PromptUpdateMessage>.ProcessMessage(PromptUpdateMessage message)
        {
            prompt = message.Prompt;
        }
    }
}