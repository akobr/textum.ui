using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using BeaverSoft.Texo.Core;
using BeaverSoft.Texo.Core.Actions;
using BeaverSoft.Texo.Core.Commands;
using BeaverSoft.Texo.Core.Configuration;
using BeaverSoft.Texo.Core.Environment;
using BeaverSoft.Texo.Core.Inputting;
using BeaverSoft.Texo.Core.Inputting.History;
using BeaverSoft.Texo.Core.Markdown.Builder;
using BeaverSoft.Texo.Core.Path;
using BeaverSoft.Texo.Core.Runtime;
using BeaverSoft.Texo.Core.View;
using Markdig.Wpf;
using StrongBeaver.Core;
using StrongBeaver.Core.Messaging;

namespace BeaverSoft.Texo.View.WPF
{
    public class WpfViewService : IViewService, IPromptableViewService, IInitialisable<TexoControl>
    {
        private const string DEFAULT_PROMPT = "initialising>";
        private const string DEFAULT_TITLE = "Texo UI";

        private readonly IWpfRenderService renderer;
        private readonly IFactory<IInputHistoryService> historyFactory;
        private readonly IActionUrlParser actionParser;

        private IExecutor executor;
        private TexoControl control;
        private IInputHistoryService history;

        private bool showWorkingPathAsPrompt;
        private string currentPrompt;
        private string currentTitle;
        private string workingDirectory;
        private string lastWorkingDirectory;

        private IHistoryItem historyItem;

        public WpfViewService(IWpfRenderService renderer, IFactory<IInputHistoryService> historyFactory)
        {
            this.renderer = renderer;
            this.historyFactory = historyFactory;
            actionParser = new ActionUrlParser();

            currentPrompt = DEFAULT_PROMPT;
            currentTitle = DEFAULT_TITLE;
            WorkingDirectory = Environment.CurrentDirectory ?? string.Empty;
        }

        public string WorkingDirectory
        {
            get => workingDirectory;
            set
            {
                value = value.RemoveEndDirectorySeparator();

                if (string.Equals(value, workingDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                workingDirectory = value;
            }
        }

        public void SetInput(string input)
        {
            control.SetInput(input.Trim());
        }

        public void AddInput(string append)
        {
            append = append.Trim();
            string input = control.GetInput();

            if (string.IsNullOrEmpty(input))
            {
                control.SetInput(append);
                return;
            }

            if (input.EndsWith(" "))
            {
                control.SetInput(input + append);
                return;
            }

            control.SetInput($"{input} {append}");
        }

        public string GetNewInput()
        {
            throw new NotImplementedException();
        }

        public void ShowProgress(int id, string name, int progress)
        {
            control.SetProgress(name, progress);
        }

        public async void Render(Input input, IImmutableList<IItem> items)
        {
            // TODO: [P2] Solve this by result processing pipeline
            if (input.Context.Key == CommandKeys.CLEAR)
            {
                control.EnableInput();
                control.SetHistoryCount(history.Count);
                return;
            }

            List<Section> sections = new List<Section>(items.Count);
            Item headerItem = BuildCommandHeaderItem(input);
            Section header = renderer.Render(headerItem);
            header.Loaded += HandleLastElementLoaded;
            sections.Add(header);

            if (items != null && items.Count > 0)
            {
                if (items.Count == 1)
                {
                    IItem firstItem = items[0];

                    if (firstItem is IStreamedItem steamItem)
                    {
                        sections.Add(await renderer.StartStreamRenderAsync(steamItem.Stream, 
                            (renderedSpan) =>
                            {
                                if (renderedSpan.Inlines.Count < 1)
                                {
                                    return;
                                }

                                if (!control.IsAutoScrollEnabled)
                                {
                                    return;   
                                }

                                Inline lastLine = renderedSpan.Inlines.LastInline;
                                if (lastLine.IsLoaded)
                                {
                                    if (!control.IsAutoScrollEnabled)
                                    {
                                        return;
                                    }

                                    control.RequestSystemScroll();
                                    lastLine.BringIntoView();
                                }
                                else
                                {
                                    lastLine.Loaded += HandleLastElementLoaded;
                                }
                            },
                            () => {
                                control.Dispatcher.Invoke(() =>
                                {
                                    control.EnableInput();
                                    control.SetHistoryCount(history.Count);
                                });
                            }));

                        control.OutputDocument.Blocks.AddRange(sections);
                        return;
                    }
                    else if (!string.IsNullOrEmpty(firstItem.Text))
                    {
                        sections.Add(renderer.Render(firstItem));
                    }
                }
                else
                {
                    foreach (IItem item in items)
                    {
                        sections.Add(renderer.Render(item));
                    }
                } 
            }
            else
            {
                sections.Add(renderer.Render(Item.AsMarkdown("> command is done")));
            }

            control.OutputDocument.Blocks.AddRange(sections);
            control.EnableInput();
            control.SetHistoryCount(history.Count);
        }

        private void HandleLastElementLoaded(object sender, RoutedEventArgs e)
        {
            FrameworkContentElement element = (FrameworkContentElement)sender;
            element.Loaded -= HandleLastElementLoaded;

            if (!control.IsAutoScrollEnabled)
            {
                return;
            }

            control.RequestSystemScroll();
            element.BringIntoView();
        }

        public void RenderIntellisense(Input input, IImmutableList<IItem> items)
        {
            if (!control.Dispatcher.CheckAccess())
            {
                control.Dispatcher.Invoke(() => RenderIntellisense(input, items));
                return;
            }

            control.IntellisenseList.Visibility = Visibility.Collapsed;
            control.IntellisenseList.Items.Clear();

            if (items == null || items.Count < 1)
            {
                return;
            }

            foreach (IItem item in items)
            {
                Section itemSection = renderer.Render(item);
                RichTextBox box = new RichTextBox()
                {
                    IsReadOnly = true,
                    IsReadOnlyCaretVisible = false,
                    IsManipulationEnabled = false,
                    IsHitTestVisible = false,
                    Margin = new Thickness(4),
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                };

                // box.SetResourceReference(Control.ForegroundProperty, "SystemBaseHighColorBrush");
                box.SetResourceReference(Control.ForegroundProperty, SystemColors.ControlTextBrushKey);
                box.Document = new FlowDocument();
                box.Document.Blocks.AddRange(itemSection.Blocks.ToList());
                control.IntellisenseList.Items.Add(new ListBoxItem() { Content = box, Tag = item });
            }

            control.IntellisenseList.Visibility = Visibility.Visible;
        }

        public void RenderProgress(IProgress progress)
        {
            throw new NotImplementedException();
        }

        public void Update(string key, IItem item)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            // TODO
        }

        public void Dispose()
        {
            // TODO
        }

        public void Initialise(IExecutor context)
        {
            executor = context;
            history = historyFactory.Create();
        }

        private void OnLinkCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private async void OnLinkExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            e.Handled = true;
            string url = e.Parameter.ToString();

            IActionContext actionContext = actionParser.Parse(url);

            if (actionContext.Name == ActionNames.INPUT_UPDATE)
            {
                UpdateInput(actionContext);
                return;
            }

            await executor.ExecuteActionAsync(url);
        }

        public void Initialise(TexoControl context)
        {
            control = context;
            InitialiseControl();
        }

        private void InitialiseControl()
        {
            control.InputChanged += TexoInputChanged;
            control.InputFinished += TexoInputFinished;
            control.KeyScrolled += TexoCommandHistoryScrolled;
            control.IntellisenseItemExecuted += TexoIntellisenseItemExecuted;

            CommandBinding linkCommandBinding = new CommandBinding(Commands.Hyperlink, OnLinkExecuted, OnLinkCanExecute);
            control.CommandBindings.Add(linkCommandBinding);

            control.Prompt = currentPrompt;
            control.Title = currentTitle;
            BuildInitialFlowDocument();
        }

        private void TexoIntellisenseItemExecuted(object sender, EventArgs e)
        {
            ListBoxItem viewItem = (ListBoxItem)control.IntellisenseList.SelectedItem;
            IItem item = (IItem)viewItem.Tag;
            control.CloseIntellisense();

            if (item.Actions.Count < 1)
            {
                return;
            }

            ILink actionLink = item.Actions.First();
            IActionContext actionContext = actionParser.Parse(actionLink.Address.AbsoluteUri);

            if (actionContext == null)
            {
                return;
            }

            if (actionContext.Name == ActionNames.INPUT_UPDATE)
            {
                UpdateInput(actionContext);
                return;
            }

            executor.ExecuteActionAsync(actionLink.Address.AbsoluteUri);
        }

        private void UpdateInput(IActionContext actionContext)
        {
            if (!actionContext.Arguments.ContainsKey(ActionParameters.INPUT))
            {
                return;
            }

            string currentInput = control.GetInput();
            string value = actionContext.Arguments[ActionParameters.INPUT];
            bool isAdd = string.IsNullOrWhiteSpace(currentInput) || char.IsWhiteSpace(currentInput[currentInput.Length - 1]);

            if (isAdd)
            {
                control.SetInput(currentInput + value);
            }
            else
            {
                int index = currentInput.LastIndexOf(" ") + 1;
                control.SetInput(currentInput.Substring(0, index) + value);
            }
        }

        private void TexoCommandHistoryScrolled(object sender, KeyScrollDirection direction)
        {
            IHistoryItem historyItem = CalculateNewHystoryItem(direction);

            if (historyItem == null)
            {
                return;
            }

            control.SetInput(historyItem.Input.ParsedInput.RawInput);
        }

        private IHistoryItem CalculateNewHystoryItem(KeyScrollDirection direction)
        {
            if (historyItem == null)
            {
                historyItem = history.GetLastInput();
            }
            else if (direction == KeyScrollDirection.Back)
            {
                if (historyItem.IsDeleted)
                {
                    while (historyItem != null && historyItem.IsDeleted)
                    {
                        historyItem = historyItem.Next;
                    }
                }
                else if (historyItem.HasPrevious)
                {
                    historyItem = historyItem.Previous;
                }
            }
            else
            {
                if (historyItem.IsDeleted)
                {
                    while (historyItem != null && historyItem.IsDeleted)
                    {
                        historyItem = historyItem.Next;
                    }
                }
                else if (historyItem.HasNext)
                {
                    historyItem = historyItem.Next;
                }
            }

            return historyItem;
        }

        private void TexoInputChanged(object sender, string input)
        {
            executor.PreProcess(input, input.Length);
        }

        private async void TexoInputFinished(object sender, string input)
        {
            control.CloseIntellisense();
            control.DisableInput();
            lastWorkingDirectory = workingDirectory;
            historyItem = null;
            await executor.ProcessAsync(input, CancellationToken.None);
        }

        private Item BuildCommandHeaderItem(Input input)
        {
            MarkdownBuilder headerBuilder = new MarkdownBuilder();
            headerBuilder.WriteLine("***");

            if (input.Context.IsValid)
            {
                WriteInputTokens(input, headerBuilder);
            }
            else
            {
                headerBuilder.Write(input.ParsedInput.RawInput);
                headerBuilder.Write(" ");
            }

            string atPath = lastWorkingDirectory;
            if (atPath[atPath.Length - 1] == '\\')
            {
                atPath += '\\';
            }

            //headerBuilder.WriteLine();
            //headerBuilder.Blockquotes(atPath);
            headerBuilder.Italic($"[{atPath}]");
            return Item.AsMarkdown(headerBuilder.ToString());
        }

        public void ProcessMessage(ISettingUpdatedMessage message)
        {
            if (control != null && !control.Dispatcher.CheckAccess())
            {
                control.Dispatcher.InvokeAsync(() => ProcessMessage(message));
                return;
            }

            showWorkingPathAsPrompt = message.Configuration.Ui.ShowWorkingPathAsPrompt;

            if (showWorkingPathAsPrompt)
            {
                SetPrompt(WorkingDirectory);
                SetTitle(DEFAULT_TITLE);
            }
            else
            {
                SetPrompt(message.Configuration.Ui.Prompt);
                SetTitle(WorkingDirectory);
            }
        }

        void IMessageBusRecipient<PromptUpdateMessage>.ProcessMessage(PromptUpdateMessage message)
        {
            if (control != null && !control.Dispatcher.CheckAccess())
            {
                control.Dispatcher.InvokeAsync(() => SetPrompt(message.Prompt));
                return;
            }

            SetPrompt(message.Prompt);
        }

        public void ProcessMessage(IVariableUpdatedMessage message)
        {
            if (control != null && !control.Dispatcher.CheckAccess())
            {
                control.Dispatcher.InvokeAsync(() => ProcessMessage(message));
                return;
            }

            control?.SetVariableCount(message.Environment.Count);

            if (message.Name != VariableNames.CURRENT_DIRECTORY)
            {
                return;
            }

            WorkingDirectory = message.NewValue;

            if (showWorkingPathAsPrompt)
            {
                SetPrompt(workingDirectory);
            }
            else
            {
                SetTitle(workingDirectory);
                SetPrompt("tu");
            }
        }

        void IMessageBusRecipient<IClearViewOutputMessage>.ProcessMessage(IClearViewOutputMessage message)
        {
            control.CleanResults();
        }

        private void SetPrompt(string prompt)
        {
            currentPrompt = $"{prompt}>";

            if (control == null)
            {
                return;
            }

            control.Prompt = currentPrompt;
        }

        private void SetTitle(string title)
        {
            currentTitle = title;

            if (control == null)
            {
                return;
            }

            control.Title = currentTitle;
        }

        //[Conditional("DEBUG")]
        private void BuildInitialFlowDocument()
        {
            control.OutputDocument.Blocks.Add(new Paragraph(new Run($"Terminal powered by Texo UI: v.{TexoEngine.Version}"))
            {
                FontSize = 12,
                FontStyle = FontStyles.Italic,
                TextAlignment = TextAlignment.Left,
                Foreground = Brushes.DimGray
            });
        }

        private static void WriteInputTokens(Input input, IMarkdownBuilder headerBuilder)
        {
            foreach (IToken token in input.Tokens)
            {
                switch (token.Type)
                {
                    case TokenTypeEnum.Query:
                        headerBuilder.Bold(token.Input);
                        break;

                    case TokenTypeEnum.Option:
                    case TokenTypeEnum.OptionList:
                        headerBuilder.Italic(token.Input);
                        break;

                    case TokenTypeEnum.Wrong:
                    case TokenTypeEnum.Unknown:
                        continue;

                    default:
                        headerBuilder.Write(token.Input);
                        break;
                }

                headerBuilder.Write(" ");
            }
        }
    }
}
