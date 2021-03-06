using BeaverSoft.Texo.Core.Streaming;
using BeaverSoft.Texo.Core.View;
using Markdig.Wpf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace BeaverSoft.Texo.View.WPF.Rendering
{
    public class WpfConsoleTextRenderService : IWpfRenderService
    {
        private static readonly int NewLineLenght = Environment.NewLine.Length;
        private readonly Regex formattingRegex = new Regex(@"\u001b\[(?<code>\d*(;\d+)*)m", RegexOptions.Compiled);

        private delegate void RenderMethod(string text);

        private Brush backgroundBrush = Brushes.Transparent;
        private Brush foregroundBrush = Brushes.Transparent;
        private FontStyle fontStyle = FontStyles.Normal;
        private FontWeight fontWeight = FontWeights.Normal;
        private TextDecorationCollection decorations = new TextDecorationCollection();
        private bool isHyperlink;

        private Span streamedContainer;
        private bool renderIsRequested;
        private bool renderInProgress;
        private int streamedPosition;
        private Action streamedOnFinished;
        private Action<Span> streamedOnAfterRender;

        public Section Render(IItem item)
        {
            Section itemSection = new Section();
            itemSection.Blocks.Add(new Paragraph(BuildText(item.Text)));
            return itemSection;
        }

        public async Task<Section> StartStreamRenderAsync(IReportableStream stream, Action<Span> onAfterRender, Action onFinish)
        {
            Section itemSection = new Section();
            itemSection.Blocks.Add(new Paragraph(await StartStreamRenderToSpanAsync(stream, onAfterRender, onFinish)));
            return itemSection;
        }

        private async Task<Span> StartStreamRenderToSpanAsync(IReportableStream stream, Action<Span> onAfterRender, Action onFinish)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (stream.IsClosed)
            {
                throw new InvalidOperationException("The text stream is already closed.");
            }

            if (streamedContainer != null)
            {
                throw new InvalidOperationException("Another stream render already in progress.");
            }

            if (stream.IsCompleted)
            {
                string allText;
                using (TextReader reader = new StreamReader(stream.Stream, Encoding.UTF8, false, 1024, true))
                {
                    allText = await reader.ReadToEndAsync();
                }
                
                Span container = BuildText(allText);
                stream.Dispose();
                onFinish?.Invoke();
                return container;
            }

            stream.StreamModified += OnStreamModified;
            stream.StreamCompleted += OnStreamCompleted;

            renderInProgress = false;
            renderIsRequested = false;
            streamedContainer = new Span();
            streamedPosition = 0;
            streamedOnAfterRender = onAfterRender;
            streamedOnFinished = onFinish;
            return streamedContainer;
        }

        private void OnStreamCompleted(object sender, EventArgs e)
        {
            if (streamedContainer == null)
            {
                return;
            }

            TryFinishStreamRender((IReportableStream)sender);
        }

        private void OnStreamModified(object sender, EventArgs e)
        {
            if (streamedContainer == null)
            {
                return;
            }

            renderIsRequested = true;

            if (renderInProgress)
            {
                return;
            }

            renderInProgress = true;
            // TODO: [P3] A swallowed exception
            Task.Run(() => RenderModifiedStreamAsync((IReportableStream)sender));
        }

        private async Task RenderModifiedStreamAsync(IReportableStream stream)
        {
            renderInProgress = true;
            renderIsRequested = false;
            string newText;

            using (TextReader reader = new StreamReader(stream.Stream, Encoding.UTF8, false, 1024, true))
            {
                newText = await reader.ReadToEndAsync();
            }

            await streamedContainer.Dispatcher.BeginInvoke(new RenderMethod(BuildStreamedText), DispatcherPriority.Background, newText);       

            if (renderIsRequested)
            {
                // TODO: [P3] A swallowed exception
                _ = RenderModifiedStreamAsync(stream);
            }
            else
            {
                renderInProgress = false;
                TryFinishStreamRender(stream);
            }
        }

        private Span BuildText(string text)
        {
            ResetFormatting();

            Span container = new Span();
            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int lastIndex = lines.Length - 1;
            int position = 0;

            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index];
                bool isLastLine = index == lastIndex;
                if (isLastLine && string.IsNullOrEmpty(line))
                {
                    break;
                }

                Span lineContainer = BuildLine(line, index, position, isLastLine, out int nextPosition);
                container.Inlines.Add(lineContainer);
                position = nextPosition;
            }

            return container;
        }

        private void BuildStreamedText(string newText)
        {
            if (newText == null || streamedContainer == null)
            {
                return;
            }

            Span container = streamedContainer;
            string[] lines = newText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            IEnumerable<string> linesToProcess = lines;
            int index = streamedContainer.Inlines.Count;
            int lastIndex = index + lines.Length - 1;
            int position = streamedPosition;
            string firstLine = lines[0];

            if (container.Inlines.Count > 0)
            {
                Span previousLine = (Span)container.Inlines.LastInline;
                LineInfo previousLineInfo = (LineInfo)previousLine.Tag;

                if (previousLineInfo.Unfinished)
                {
                    string lineText = GetTextFromSpan(previousLine) + firstLine;
                    Span swapLine = BuildLine(lineText, previousLineInfo.Index, previousLineInfo.PositionInStream, lines.Length == 1, out position);
                    container.Inlines.Remove(previousLine);
                    container.Inlines.Add(swapLine);
                    linesToProcess = linesToProcess.Skip(1);
                    lastIndex--;
                }
            }

            foreach (string line in linesToProcess)
            {
                bool isLastLine = index == lastIndex;
                if (isLastLine && string.IsNullOrEmpty(line))
                {
                    break;
                }

                Span lineContainer = BuildLine(line, index, position, isLastLine, out int nextPosition);
                container.Inlines.Add(lineContainer);
                index++;
                position = nextPosition;
            }

            streamedOnAfterRender?.Invoke(container);
        }

        private Span BuildLine(string line, int lineIndex, int linePositionInStream, bool isLastLine, out int positionInStream)
        {
            Span container = new Span();            
            Match match;
            int index = 0;
            int length = 0;

            positionInStream = linePositionInStream + line.Length;
            int carrierReturnIndex = line.LastIndexOf("\r");

            if (carrierReturnIndex >= 0)
            {
                line = line.Substring(carrierReturnIndex + 1);
            }

            while ((match = formattingRegex.Match(line, index)).Success)
            {
                if (index < match.Index)
                {
                    int sectionLength = match.Index - index;
                    length += sectionLength;
                    container.Inlines.Add(CreateInline(line.Substring(index, sectionLength)));
                }

                UpdateFormatting(match.Groups["code"].Value);
                index = match.Index + match.Length;
            }

            if (index < line.Length)
            {
                int sectionLength = line.Length - index;
                length += sectionLength;
                container.Inlines.Add(CreateInline(line.Substring(index, sectionLength)));
            }

            if (!isLastLine)
            {
                container.Inlines.Add(new Run(Environment.NewLine));
                length += NewLineLenght;
                positionInStream += NewLineLenght; // TODO: [P2] this will be wrong with linux line breaks on windows
            }
            
            container.Tag = new LineInfo() { Index = lineIndex, Length = length, PositionInStream = linePositionInStream, Unfinished = isLastLine };
            return container;
        }

        private string GetTextFromSpan(Span span)
        {
            StringBuilder builder = new StringBuilder();

            foreach (Inline child in span.Inlines)
            {
                switch (child)
                {
                    case Run run:
                        builder.Append(run.Text);
                        break;

                    case Hyperlink link:
                        builder.Append(((Run)link.Inlines.FirstInline).Text);
                        break;

                }
            }

            return builder.ToString();
        }

        private void UpdateFormatting(string value)
        {
            // TODO: [P3] needs refactoring
            if (value.Length > 9)
            {
                if(value.StartsWith("38;2"))
                {
                    string[] values = value.Split(';');
                    foregroundBrush = new SolidColorBrush(Color.FromArgb(255, byte.Parse(values[2]), byte.Parse(values[3]), byte.Parse(values[4])));
                    return;
                }
            }

            switch (value)
            {
                case "0":
                case "":
                    ResetFormatting();
                    break;

                case "1":
                    fontWeight = FontWeights.Bold;
                    break;

                case "3":
                    fontStyle = FontStyles.Italic;
                    break;

                case "4":
                    decorations.Add(TextDecorations.Underline);
                    break;

                case "9":
                    decorations.Add(TextDecorations.Strikethrough);
                    break;

                case "30":
                    foregroundBrush = Brushes.Black;
                    break;

                case "30;1":
                    foregroundBrush = Brushes.Gray;
                    break;

                case "31":
                case "31;1":
                    foregroundBrush = Brushes.DarkSalmon;                    
                    break;

                case "32":
                    foregroundBrush = Brushes.Green;
                    break;

                case "32;1":
                    foregroundBrush = Brushes.LightGreen;
                    break;

                case "33":
                    foregroundBrush = Brushes.Yellow;
                    break;

                case "33;1":
                    foregroundBrush = Brushes.LightGoldenrodYellow;
                    break;

                case "34":
                    foregroundBrush = Brushes.Blue;
                    break;

                case "34;1":
                    foregroundBrush = Brushes.LightBlue;
                    break;

                case "35":
                case "35;1":
                    foregroundBrush = Brushes.Magenta;
                    break;

                case "36":
                    foregroundBrush = Brushes.DarkCyan;
                    break;

                case "36;1":
                    foregroundBrush = Brushes.Cyan;
                    break;

                case "37":
                    foregroundBrush = Brushes.White;
                    break;

                case "40":
                    backgroundBrush = Brushes.Black;
                    break;

                case "41":
                    backgroundBrush = Brushes.Red;
                    break;

                case "42":
                    backgroundBrush = Brushes.Green;
                    break;

                case "43":
                    backgroundBrush = Brushes.Yellow;
                    break;

                case "44":
                    backgroundBrush = Brushes.Blue;
                    break;

                case "45":
                    backgroundBrush = Brushes.Magenta;
                    break;

                case "46":
                    backgroundBrush = Brushes.Cyan;
                    break;

                case "47":
                    backgroundBrush = Brushes.White;
                    break;

                case "998":
                    isHyperlink = false;
                    break;

                case "999":
                    isHyperlink = true;
                    break;
            }
        }

        private void ResetFormatting()
        {
            backgroundBrush = Brushes.Transparent;
            foregroundBrush = Brushes.Transparent;
            fontStyle = FontStyles.Normal;
            fontWeight = FontWeights.Normal;
            decorations.Clear();
        }

        private Inline CreateInline(string text)
        {
            Inline result;

            if (isHyperlink)
            {
                string[] values = text.Split('|');
                string title = values[0].Trim();
                string url = values[1].Trim();

                Hyperlink link = new Hyperlink()
                {
                    Command = Commands.Hyperlink,
                    CommandParameter = url,
                };

                link.SetResourceReference(FrameworkContentElement.StyleProperty, Styles.HyperlinkStyleKey);

                link.Inlines.Add(title);
                link.NavigateUri = new Uri(url);
                result = link;
            }
            else
            {
                result = new Run(text);
                result.FontWeight = fontWeight;
                result.TextDecorations = decorations.Clone();
            }

            if (foregroundBrush != Brushes.Transparent)
            {
                result.Foreground = foregroundBrush;
            }
            
            result.Background = backgroundBrush;
            result.FontStyle = fontStyle;         

            return result;
        }

        private async void TryFinishStreamRender(IReportableStream stream)
        {
            if (!stream.IsCompleted)
            {
                return;
            }

            stream.StreamModified -= OnStreamModified;
            stream.StreamCompleted -= OnStreamCompleted;

            if (renderInProgress)
            {
                return;
            }

            await streamedContainer.Dispatcher.BeginInvoke(
                    new SetEmptyStream(SetEmptyCommandStream),
                    DispatcherPriority.ApplicationIdle,
                    streamedContainer);

            streamedContainer = null;
            stream.Dispose();
            streamedOnAfterRender = null;
            streamedOnFinished?.Invoke();
            streamedOnFinished = null;

            _ = Task.Run(() =>
            {
                GC.Collect(3, GCCollectionMode.Optimized, false);
            });

            // Warn: This can cause freeze of an entire application
            // GC.Collect(3, GCCollectionMode.Forced, true);
        }

        private void SetEmptyCommandStream(Span target)
        {
            if (target == null || !target.IsInitialized || target.Parent == null)
            {
                return;
            }

            if (target.Inlines.Count > 0)
            {
                return;
            }

            target.Inlines.Add(new Run("command is done") { Foreground = Brushes.Gray });
        }

        private delegate void SetEmptyStream(Span target);

        private struct LineInfo
        {
            public int Index;
            public int Length;
            public int PositionInStream;
            public bool Unfinished;
        }
    }
}
