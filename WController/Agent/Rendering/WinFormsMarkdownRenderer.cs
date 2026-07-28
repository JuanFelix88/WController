using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WController.Agent.Rendering;

internal sealed class WinFormsMarkdownRenderer : IMarkdownRenderer
{
    private static readonly Color BackgroundColor = Color.FromArgb(30, 30, 30);
    private static readonly Color TextColor = Color.FromArgb(220, 220, 220);
    private static readonly Color CodeBackgroundColor = Color.FromArgb(45, 45, 45);
    private static readonly Color AccentColor = Color.FromArgb(86, 156, 214);
    private static readonly Color QuoteBorderColor = Color.FromArgb(64, 64, 64);

    private static readonly Regex HeaderRegex = new Regex(@"^\s*(#{1,6})\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex OrderedListRegex = new Regex(@"^(\s*)(\d+)\.\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex BulletListRegex = new Regex(@"^(\s*)[-*]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex HorizontalRuleRegex = new Regex(@"^\s*([-*_])(?:\s*\1){2,}\s*$", RegexOptions.Compiled);
    private static readonly Regex LinkRegex = new Regex(@"\[(?<text>[^\]]+)\]\((?<url>[^)\s]+(?:\s+""[^""]*"")?)\)", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new Regex(@"(\s+)", RegexOptions.Compiled);

    private const string RendererHostName = "__MarkdownRendererHost";

    private readonly object syncRoot = new object();
    private readonly Dictionary<Control, RenderState> renderStates = new Dictionary<Control, RenderState>();
    private readonly Dictionary<string, Font> fontCache = new Dictionary<string, Font>(StringComparer.Ordinal);

    public void Render(string markdown, Control container)
    {
        if (container is null)
        {
            throw new ArgumentNullException(nameof(container));
        }

        ExecuteOnUiThread(container, () =>
        {
            RenderState renderState;
            string snapshot;

            lock (syncRoot)
            {
                renderState = GetOrCreateRenderState(container);
                renderState.Markdown.Clear();
                if (!string.IsNullOrEmpty(markdown))
                {
                    renderState.Markdown.Append(markdown);
                }

                snapshot = renderState.Markdown.ToString();
            }

            RenderSnapshot(container, renderState, snapshot, false);
        });
    }

    public void RenderAppend(string markdownDelta, Control container)
    {
        if (container is null)
        {
            throw new ArgumentNullException(nameof(container));
        }

        ExecuteOnUiThread(container, () =>
        {
            RenderState renderState;
            string snapshot;

            lock (syncRoot)
            {
                renderState = GetOrCreateRenderState(container);
                if (!string.IsNullOrEmpty(markdownDelta))
                {
                    renderState.Markdown.Append(markdownDelta);
                }

                snapshot = renderState.Markdown.ToString();
            }

            RenderSnapshot(container, renderState, snapshot, true);
        });
    }

    public void Clear(Control container)
    {
        if (container is null)
        {
            throw new ArgumentNullException(nameof(container));
        }

        ExecuteOnUiThread(container, () =>
        {
            lock (syncRoot)
            {
                if (renderStates.TryGetValue(container, out RenderState? renderState))
                {
                    renderState.Markdown.Clear();
                    if (renderState.Host is not null && !renderState.Host.IsDisposed)
                    {
                        renderState.Host.Controls.Clear();
                    }

                    return;
                }
            }

            if (container is FlowLayoutPanel flowLayoutPanel)
            {
                flowLayoutPanel.Controls.Clear();
                return;
            }

            FlowLayoutPanel? existingHost = FindExistingHost(container);
            if (existingHost is not null)
            {
                existingHost.Controls.Clear();
            }
        });
    }

    private void RenderSnapshot(Control container, RenderState renderState, string markdown, bool scrollToBottom)
    {
        FlowLayoutPanel host = EnsureHost(container, renderState);
        int contentWidth = GetContentWidth(host);
        IReadOnlyList<MarkdownBlock> blocks = ParseBlocks(markdown);

        renderState.IsRendering = true;
        host.SuspendLayout();
        try
        {
            host.Controls.Clear();
            foreach (MarkdownBlock block in blocks)
            {
                Control control = CreateBlockControl(block, contentWidth);
                host.Controls.Add(control);
            }

            if (host.Controls.Count == 0)
            {
                host.Controls.Add(CreateSpacer(contentWidth, 1));
            }
        }
        finally
        {
            host.ResumeLayout(true);
            renderState.IsRendering = false;
        }

        if (scrollToBottom && host.Controls.Count > 0)
        {
            host.ScrollControlIntoView(host.Controls[host.Controls.Count - 1]);
        }
    }

    private FlowLayoutPanel EnsureHost(Control container, RenderState renderState)
    {
        FlowLayoutPanel host;
        if (container is FlowLayoutPanel flowLayoutPanel)
        {
            host = flowLayoutPanel;
        }
        else
        {
            host = renderState.Host ?? FindExistingHost(container) ?? CreateHostPanel();
            if (!ReferenceEquals(host.Parent, container))
            {
                container.Controls.Add(host);
            }
        }

        ConfigureHost(container, host);
        renderState.Host = host;
        return host;
    }

    private static FlowLayoutPanel? FindExistingHost(Control container)
    {
        foreach (Control child in container.Controls)
        {
            FlowLayoutPanel? host = child as FlowLayoutPanel;
            if (host is not null && string.Equals(host.Name, RendererHostName, StringComparison.Ordinal))
            {
                return host;
            }
        }

        return null;
    }

    private static FlowLayoutPanel CreateHostPanel()
    {
        return new FlowLayoutPanel
        {
            Name = RendererHostName,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(16),
            AutoScroll = true,
            WrapContents = false,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = false,
            TabStop = false,
            BackColor = BackgroundColor,
        };
    }

    private static void ConfigureHost(Control container, FlowLayoutPanel host)
    {
        container.BackColor = BackgroundColor;
        host.BackColor = BackgroundColor;
        host.FlowDirection = FlowDirection.TopDown;
        host.WrapContents = false;
        host.AutoScroll = true;
        host.Margin = Padding.Empty;
        host.Padding = new Padding(16);
        host.TabStop = false;

        if (!ReferenceEquals(container, host))
        {
            host.Dock = DockStyle.Fill;
        }
    }

    private int GetContentWidth(FlowLayoutPanel host)
    {
        int width = host.ClientSize.Width;
        if (width <= 0)
        {
            width = host.Width;
        }

        width -= host.Padding.Horizontal;
        if (host.VerticalScroll.Visible)
        {
            width -= SystemInformation.VerticalScrollBarWidth;
        }

        return Math.Max(220, width - 4);
    }

    private RenderState GetOrCreateRenderState(Control container)
    {
        if (renderStates.TryGetValue(container, out RenderState? existingState))
        {
            return existingState;
        }

        RenderState renderState = new RenderState();
        renderStates.Add(container, renderState);
        container.Disposed += HandleContainerDisposed;
        return renderState;
    }

    private void HandleContainerDisposed(object? sender, EventArgs e)
    {
        Control? container = sender as Control;
        if (container is null)
        {
            return;
        }

        lock (syncRoot)
        {
            renderStates.Remove(container);
        }
    }

    private static void ExecuteOnUiThread(Control container, Action action)
    {
        if (container.IsDisposed)
        {
            return;
        }

        if (container.InvokeRequired)
        {
            container.Invoke(action);
            return;
        }

        action();
    }

    private IReadOnlyList<MarkdownBlock> ParseBlocks(string markdown)
    {
        List<MarkdownBlock> blocks = new List<MarkdownBlock>();
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return blocks;
        }

        string normalized = NormalizeLineEndings(markdown);
        string[] lines = normalized.Split('\n');

        bool insideCodeBlock = false;
        string? codeLanguage = null;
        StringBuilder codeBuilder = new StringBuilder();
        StringBuilder paragraphBuilder = new StringBuilder();
        List<ListItemBlock>? currentListItems = null;
        bool currentListOrdered = false;
        List<string>? quoteLines = null;

        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            string trimmed = line.Trim();

            if (insideCodeBlock)
            {
                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    blocks.Add(new CodeBlock(codeLanguage, TrimTrailingNewLine(codeBuilder.ToString())));
                    insideCodeBlock = false;
                    codeLanguage = null;
                    codeBuilder.Clear();
                    continue;
                }

                codeBuilder.AppendLine(line);
                continue;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph(blocks, paragraphBuilder);
                FlushList(blocks, ref currentListItems, ref currentListOrdered);
                FlushQuote(blocks, ref quoteLines);

                insideCodeBlock = true;
                codeLanguage = trimmed.Length > 3 ? trimmed.Substring(3).Trim() : string.Empty;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph(blocks, paragraphBuilder);
                FlushList(blocks, ref currentListItems, ref currentListOrdered);
                FlushQuote(blocks, ref quoteLines);
                continue;
            }

            Match orderedMatch = OrderedListRegex.Match(line);
            if (orderedMatch.Success)
            {
                FlushParagraph(blocks, paragraphBuilder);
                FlushQuote(blocks, ref quoteLines);

                if (currentListItems is null || !currentListOrdered)
                {
                    FlushList(blocks, ref currentListItems, ref currentListOrdered);
                    currentListItems = new List<ListItemBlock>();
                    currentListOrdered = true;
                }

                string indent = orderedMatch.Groups[1].Value;
                string number = orderedMatch.Groups[2].Value;
                string text = orderedMatch.Groups[3].Value.Trim();
                currentListItems.Add(new ListItemBlock(number + ".", text, GetIndentLevel(indent)));
                continue;
            }

            Match bulletMatch = BulletListRegex.Match(line);
            if (bulletMatch.Success)
            {
                FlushParagraph(blocks, paragraphBuilder);
                FlushQuote(blocks, ref quoteLines);

                if (currentListItems is null || currentListOrdered)
                {
                    FlushList(blocks, ref currentListItems, ref currentListOrdered);
                    currentListItems = new List<ListItemBlock>();
                    currentListOrdered = false;
                }

                string indent = bulletMatch.Groups[1].Value;
                string text = bulletMatch.Groups[2].Value.Trim();
                currentListItems.Add(new ListItemBlock("•", text, GetIndentLevel(indent)));
                continue;
            }

            if (trimmed.StartsWith(">", StringComparison.Ordinal))
            {
                FlushParagraph(blocks, paragraphBuilder);
                FlushList(blocks, ref currentListItems, ref currentListOrdered);

                if (quoteLines is null)
                {
                    quoteLines = new List<string>();
                }

                quoteLines.Add(ExtractQuoteContent(line));
                continue;
            }

            if (HorizontalRuleRegex.IsMatch(line))
            {
                FlushParagraph(blocks, paragraphBuilder);
                FlushList(blocks, ref currentListItems, ref currentListOrdered);
                FlushQuote(blocks, ref quoteLines);
                blocks.Add(HorizontalRuleBlock.Instance);
                continue;
            }

            Match headerMatch = HeaderRegex.Match(line);
            if (headerMatch.Success)
            {
                FlushParagraph(blocks, paragraphBuilder);
                FlushList(blocks, ref currentListItems, ref currentListOrdered);
                FlushQuote(blocks, ref quoteLines);

                int level = headerMatch.Groups[1].Value.Length;
                string text = headerMatch.Groups[2].Value.Trim();
                blocks.Add(new HeaderBlock(level, text));
                continue;
            }

            FlushList(blocks, ref currentListItems, ref currentListOrdered);
            FlushQuote(blocks, ref quoteLines);
            AppendParagraphLine(paragraphBuilder, line.Trim());
        }

        if (insideCodeBlock)
        {
            blocks.Add(new CodeBlock(codeLanguage, TrimTrailingNewLine(codeBuilder.ToString())));
        }

        FlushParagraph(blocks, paragraphBuilder);
        FlushList(blocks, ref currentListItems, ref currentListOrdered);
        FlushQuote(blocks, ref quoteLines);

        return blocks;
    }

    private Control CreateBlockControl(MarkdownBlock block, int width)
    {
        if (block is HeaderBlock header)
        {
            return CreateHeaderControl(header, width);
        }

        if (block is ParagraphBlock paragraph)
        {
            return CreateParagraphControl(paragraph, width);
        }

        if (block is CodeBlock code)
        {
            return CreateCodeBlockControl(code, width);
        }

        if (block is ListBlock list)
        {
            return CreateListControl(list, width);
        }

        if (block is HorizontalRuleBlock)
        {
            return CreateHorizontalRuleControl(width);
        }

        if (block is QuoteBlock quote)
        {
            return CreateQuoteControl(quote, width);
        }

        return CreateSpacer(width, 1);
    }

    private Control CreateHeaderControl(HeaderBlock header, int width)
    {
        float fontSize;
        switch (header.Level)
        {
            case 1:
                fontSize = 18f;
                break;
            case 2:
                fontSize = 16f;
                break;
            case 3:
                fontSize = 14f;
                break;
            case 4:
                fontSize = 13f;
                break;
            case 5:
                fontSize = 12f;
                break;
            default:
                fontSize = 11f;
                break;
        }

        Control control = CreateInlineTextControl(header.Text, width, fontSize, FontStyle.Bold, BackgroundColor);
        control.Margin = new Padding(0, 0, 0, header.Level <= 2 ? 12 : 8);
        return control;
    }

    private Control CreateParagraphControl(ParagraphBlock paragraph, int width)
    {
        Control control = CreateInlineTextControl(paragraph.Text, width, 11f, FontStyle.Regular, BackgroundColor);
        control.Margin = new Padding(0, 0, 0, 10);
        return control;
    }

    private Control CreateCodeBlockControl(CodeBlock block, int width)
    {
        RoundedPanel panel = new RoundedPanel(8)
        {
            Width = width,
            Height = 1,
            BackColor = CodeBackgroundColor,
            Margin = new Padding(0, 0, 0, 12),
            Padding = Padding.Empty,
        };

        int y = 10;
        if (!string.IsNullOrWhiteSpace(block.Language))
        {
            Label languageLabel = new Label
            {
                AutoSize = true,
                Location = new Point(14, y),
                Text = block.Language,
                Font = GetTextFont(8.5f, FontStyle.Bold),
                ForeColor = AccentColor,
                BackColor = CodeBackgroundColor,
                Margin = Padding.Empty,
                UseMnemonic = false,
            };

            panel.Controls.Add(languageLabel);
            y = languageLabel.Bottom + 6;
        }

        int codeWidth = Math.Max(120, width - 28);
        RichTextBox codeBox = new RichTextBox
        {
            Location = new Point(14, y),
            Width = codeWidth,
            Height = MeasureCodeHeight(block.Code, 10f),
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.None,
            ShortcutsEnabled = false,
            WordWrap = false,
            Font = GetCodeFont(10f, FontStyle.Regular),
            BackColor = CodeBackgroundColor,
            ForeColor = TextColor,
            TabStop = false,
            Text = string.IsNullOrEmpty(block.Code) ? string.Empty : block.Code,
        };

        panel.Controls.Add(codeBox);
        panel.Height = codeBox.Bottom + 10;
        return panel;
    }

    private Control CreateListControl(ListBlock list, int width)
    {
        Panel panel = new Panel
        {
            Width = width,
            Height = 1,
            BackColor = BackgroundColor,
            Margin = new Padding(0, 4, 0, 10),
            Padding = Padding.Empty,
        };

        int y = 0;
        int baseIndent = 12;
        foreach (ListItemBlock item in list.Items)
        {
            int indent = baseIndent + item.IndentLevel * 20;
            int markerWidth = list.Ordered ? 28 : 20;

            Label marker = new Label
            {
                AutoSize = true,
                Location = new Point(indent, y + 2),
                Text = item.Marker,
                Font = GetTextFont(10.5f, FontStyle.Bold),
                ForeColor = AccentColor,
                BackColor = BackgroundColor,
                Margin = Padding.Empty,
                UseMnemonic = false,
            };

            int contentWidth = Math.Max(120, width - indent - markerWidth - 16);
            Control content = CreateInlineTextControl(item.Text, contentWidth, 10.5f, FontStyle.Regular, BackgroundColor);
            content.Location = new Point(indent + markerWidth, y + 1);
            content.Margin = Padding.Empty;

            panel.Controls.Add(marker);
            panel.Controls.Add(content);
            y = Math.Max(marker.Bottom, content.Bottom) + 6;
        }

        panel.Height = Math.Max(y, 1);
        return panel;
    }

    private Control CreateHorizontalRuleControl(int width)
    {
        Panel wrapper = new Panel
        {
            Width = width,
            Height = 11,
            BackColor = BackgroundColor,
            Margin = new Padding(0, 2, 0, 12),
            Padding = Padding.Empty,
        };

        Panel line = new Panel
        {
            Location = new Point(0, 5),
            Width = width,
            Height = 1,
            BackColor = QuoteBorderColor,
            Margin = Padding.Empty,
        };

        wrapper.Controls.Add(line);
        return wrapper;
    }

    private Control CreateQuoteControl(QuoteBlock quote, int width)
    {
        RoundedPanel panel = new RoundedPanel(6)
        {
            Width = width,
            Height = 1,
            BackColor = Color.FromArgb(36, 36, 36),
            Margin = new Padding(0, 0, 0, 12),
            Padding = Padding.Empty,
        };

        Panel border = new Panel
        {
            Location = new Point(0, 0),
            Width = 4,
            Height = 1,
            BackColor = QuoteBorderColor,
            Margin = Padding.Empty,
        };
        panel.Controls.Add(border);

        int y = 8;
        int contentWidth = Math.Max(120, width - 28);
        for (int index = 0; index < quote.Lines.Count; index++)
        {
            string line = quote.Lines[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                y += 6;
                continue;
            }

            Control content = CreateInlineTextControl(line, contentWidth, 11f, FontStyle.Italic, Color.FromArgb(36, 36, 36));
            content.Location = new Point(18, y);
            content.Margin = Padding.Empty;
            panel.Controls.Add(content);
            y = content.Bottom + 4;
        }

        if (y <= 8)
        {
            y = GetTextFont(11f, FontStyle.Italic).Height + 12;
        }

        border.Height = Math.Max(1, y - 4);
        panel.Height = y + 6;
        return panel;
    }

    private Control CreateInlineTextControl(string text, int width, float fontSize, FontStyle baseStyle, Color backgroundColor)
    {
        int availableWidth = Math.Max(120, width);
        string safeText = text ?? string.Empty;
        List<InlineSegment> segments = ParseInlineSegments(safeText);

        if (segments.Count == 0)
        {
            return CreatePlainLabel(string.Empty, availableWidth, fontSize, baseStyle, backgroundColor);
        }

        if (ContainsLink(segments))
        {
            return CreateInlineFlowPanel(segments, availableWidth, fontSize, baseStyle, backgroundColor);
        }

        if (IsPlainTextOnly(segments))
        {
            return CreatePlainLabel(segments[0].Text, availableWidth, fontSize, baseStyle, backgroundColor);
        }

        return CreateFormattedRichTextBox(segments, availableWidth, fontSize, baseStyle, backgroundColor);
    }

    private Control CreateInlineFlowPanel(IReadOnlyList<InlineSegment> segments, int width, float fontSize, FontStyle baseStyle, Color backgroundColor)
    {
        FlowLayoutPanel panel = new FlowLayoutPanel
        {
            Width = width,
            Height = 1,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = backgroundColor,
            TabStop = false,
        };

        panel.SuspendLayout();
        try
        {
            foreach (InlineSegment segment in segments)
            {
                if (string.IsNullOrEmpty(segment.Text))
                {
                    continue;
                }

                if (segment.IsLink)
                {
                    panel.Controls.Add(CreateLinkLabel(segment, fontSize, baseStyle, width, backgroundColor));
                    continue;
                }

                if (segment.IsCode)
                {
                    panel.Controls.Add(CreateInlineCodeLabel(segment.Text, fontSize, width));
                    continue;
                }

                AddTextRunControls(panel, segment, fontSize, baseStyle, backgroundColor);
            }
        }
        finally
        {
            panel.ResumeLayout(true);
        }

        Size preferredSize = panel.GetPreferredSize(new Size(width, 0));
        panel.Height = Math.Max(preferredSize.Height, GetTextFont(fontSize, baseStyle).Height + 4);
        return panel;
    }

    private void AddTextRunControls(FlowLayoutPanel panel, InlineSegment segment, float fontSize, FontStyle baseStyle, Color backgroundColor)
    {
        Font font = GetTextFont(fontSize, CombineStyles(baseStyle, segment.Bold, segment.Italic));
        foreach (string token in SplitTextTokens(segment.Text))
        {
            if (string.IsNullOrEmpty(token))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                int spacerWidth = MeasureWhitespaceWidth(token, font);
                if (spacerWidth > 0)
                {
                    panel.Controls.Add(CreateSpacer(spacerWidth, Math.Max(1, font.Height / 2)));
                }

                continue;
            }

            Label label = new Label
            {
                AutoSize = true,
                Text = token,
                Font = font,
                ForeColor = TextColor,
                BackColor = backgroundColor,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                UseMnemonic = false,
            };

            panel.Controls.Add(label);
        }
    }

    private Control CreateFormattedRichTextBox(IReadOnlyList<InlineSegment> segments, int width, float fontSize, FontStyle baseStyle, Color backgroundColor)
    {
        RichTextBox richTextBox = new RichTextBox
        {
            Width = width,
            Height = 1,
            BorderStyle = BorderStyle.None,
            ReadOnly = true,
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.None,
            ShortcutsEnabled = false,
            WordWrap = true,
            Font = GetTextFont(fontSize, baseStyle),
            BackColor = backgroundColor,
            ForeColor = TextColor,
            Margin = Padding.Empty,
            TabStop = false,
            Rtf = BuildInlineRtf(segments, fontSize, baseStyle),
        };

        richTextBox.Height = MeasureWrappedTextHeight(FlattenSegments(segments), GetTextFont(fontSize, baseStyle), width) + 8;
        return richTextBox;
    }

    private Label CreatePlainLabel(string text, int width, float fontSize, FontStyle style, Color backgroundColor)
    {
        return new Label
        {
            AutoSize = true,
            MaximumSize = new Size(width, 0),
            Text = text,
            Font = GetTextFont(fontSize, style),
            ForeColor = TextColor,
            BackColor = backgroundColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            UseMnemonic = false,
        };
    }

    private LinkLabel CreateLinkLabel(InlineSegment segment, float fontSize, FontStyle baseStyle, int width, Color backgroundColor)
    {
        LinkLabel linkLabel = new LinkLabel
        {
            AutoSize = true,
            MaximumSize = new Size(width, 0),
            Text = segment.Text,
            Font = GetTextFont(fontSize, CombineStyles(baseStyle, segment.Bold, segment.Italic)),
            LinkColor = AccentColor,
            ActiveLinkColor = AccentColor,
            VisitedLinkColor = AccentColor,
            ForeColor = AccentColor,
            BackColor = backgroundColor,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            LinkBehavior = LinkBehavior.HoverUnderline,
            UseMnemonic = false,
        };

        if (!string.IsNullOrWhiteSpace(segment.LinkUrl))
        {
            linkLabel.Links.Clear();
            linkLabel.Links.Add(0, segment.Text.Length, segment.LinkUrl);
            linkLabel.LinkClicked += HandleLinkClicked;
        }

        return linkLabel;
    }

    private void HandleLinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (!(e.Link.LinkData is string url) || string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
        }
    }

    private Control CreateInlineCodeLabel(string text, float fontSize, int width)
    {
        RoundedPanel pill = new RoundedPanel(4)
        {
            AutoSize = false,
            BackColor = CodeBackgroundColor,
            Margin = new Padding(1, 0, 1, 0),
            Padding = Padding.Empty,
        };

        Label label = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(Math.Max(60, width - 10), 0),
            Text = text,
            Font = GetCodeFont(fontSize, FontStyle.Regular),
            ForeColor = TextColor,
            BackColor = CodeBackgroundColor,
            Margin = Padding.Empty,
            Padding = new Padding(5, 3, 5, 3),
            UseMnemonic = false,
            Location = new Point(0, 0),
        };

        pill.Controls.Add(label);
        pill.Width = label.PreferredWidth + 2;
        pill.Height = label.PreferredHeight + 2;
        return pill;
    }

    private static bool ContainsLink(IReadOnlyList<InlineSegment> segments)
    {
        for (int index = 0; index < segments.Count; index++)
        {
            if (segments[index].IsLink)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPlainTextOnly(IReadOnlyList<InlineSegment> segments)
    {
        return segments.Count == 1
            && !segments[0].Bold
            && !segments[0].Italic
            && !segments[0].IsCode
            && !segments[0].IsLink;
    }

    private List<InlineSegment> ParseInlineSegments(string text)
    {
        List<InlineSegment> segments = new List<InlineSegment>();
        if (string.IsNullOrEmpty(text))
        {
            return segments;
        }

        int index = 0;
        while (index < text.Length)
        {
            Match linkMatch = LinkRegex.Match(text, index);
            if (linkMatch.Success && linkMatch.Index == index)
            {
                string linkText = linkMatch.Groups["text"].Value;
                string linkUrl = CleanLinkUrl(linkMatch.Groups["url"].Value);
                segments.Add(new InlineSegment(linkText, false, false, false, linkUrl));
                index += linkMatch.Length;
                continue;
            }

            int nextLinkIndex = linkMatch.Success ? linkMatch.Index : text.Length;
            string plainChunk = text.Substring(index, nextLinkIndex - index);
            AddNonLinkSegments(segments, plainChunk);
            index = nextLinkIndex;
        }

        return segments;
    }

    private void AddNonLinkSegments(List<InlineSegment> segments, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        StringBuilder buffer = new StringBuilder();
        bool bold = false;
        bool italic = false;
        bool code = false;

        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];

            if (current == '`')
            {
                FlushInlineBuffer(segments, buffer, bold, italic, code);
                code = !code;
                continue;
            }

            if (!code && current == '*' && index + 1 < text.Length && text[index + 1] == '*')
            {
                FlushInlineBuffer(segments, buffer, bold, italic, code);
                bold = !bold;
                index++;
                continue;
            }

            if (!code && current == '*')
            {
                FlushInlineBuffer(segments, buffer, bold, italic, code);
                italic = !italic;
                continue;
            }

            buffer.Append(current);
        }

        FlushInlineBuffer(segments, buffer, bold, italic, code);
    }

    private static void FlushInlineBuffer(List<InlineSegment> segments, StringBuilder buffer, bool bold, bool italic, bool code)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        segments.Add(new InlineSegment(buffer.ToString(), bold, italic, code, null));
        buffer.Clear();
    }

    private string BuildInlineRtf(IReadOnlyList<InlineSegment> segments, float fontSize, FontStyle baseStyle)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(@"{\rtf1\ansi\deff0");
        builder.Append(@"{\fonttbl{\f0 Segoe UI;}{\f1 Consolas;}}");
        builder.Append(@"{\colortbl ;");
        AppendRtfColor(builder, TextColor);
        AppendRtfColor(builder, CodeBackgroundColor);
        builder.Append('}');
        builder.Append(@"\viewkind4\uc1\pard\cf1 ");

        for (int index = 0; index < segments.Count; index++)
        {
            InlineSegment segment = segments[index];
            bool isBold = segment.Bold || (baseStyle & FontStyle.Bold) == FontStyle.Bold;
            bool isItalic = segment.Italic || (baseStyle & FontStyle.Italic) == FontStyle.Italic;

            builder.Append(segment.IsCode ? @"\f1" : @"\f0");
            builder.Append(@"\fs");
            builder.Append((int)Math.Round(fontSize * 2f));
            builder.Append(isBold ? @"\b" : @"\b0");
            builder.Append(isItalic ? @"\i" : @"\i0");
            builder.Append(segment.IsCode ? @"\highlight2 " : @"\highlight0 ");
            builder.Append(EscapeRtf(segment.Text));
            builder.Append(' ');
            if (segment.IsCode)
            {
                builder.Append(@"\highlight0 ");
            }
        }

        builder.Append(@"\par}");
        return builder.ToString();
    }

    private static void AppendRtfColor(StringBuilder builder, Color color)
    {
        builder.Append(@"\red");
        builder.Append(color.R);
        builder.Append(@"\green");
        builder.Append(color.G);
        builder.Append(@"\blue");
        builder.Append(color.B);
        builder.Append(';');
    }

    private static string EscapeRtf(string text)
    {
        StringBuilder builder = new StringBuilder(text.Length + 16);
        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            switch (current)
            {
                case '\\':
                    builder.Append(@"\\");
                    break;
                case '{':
                    builder.Append(@"\{");
                    break;
                case '}':
                    builder.Append(@"\}");
                    break;
                case '\r':
                    break;
                case '\n':
                    builder.Append(@"\line ");
                    break;
                default:
                    if (current <= 0x7f)
                    {
                        builder.Append(current);
                    }
                    else
                    {
                        builder.Append(@"\u");
                        builder.Append((int)current);
                        builder.Append('?');
                    }
                    break;
            }
        }

        return builder.ToString();
    }

    private Font GetTextFont(float size, FontStyle style)
    {
        return GetFont("Segoe UI", size, style);
    }

    private Font GetCodeFont(float size, FontStyle style)
    {
        return GetFont("Consolas", size, style);
    }

    private Font GetFont(string family, float size, FontStyle style)
    {
        string key = family + "|" + size.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "|" + (int)style;
        lock (syncRoot)
        {
            if (!fontCache.TryGetValue(key, out Font? font))
            {
                font = new Font(family, size, style, GraphicsUnit.Point);
                fontCache.Add(key, font);
            }

            return font;
        }
    }

    private int MeasureWrappedTextHeight(string text, Font font, int width)
    {
        string value = string.IsNullOrEmpty(text) ? " " : text;
        Size size = TextRenderer.MeasureText(
            value + " ",
            font,
            new Size(Math.Max(1, width), int.MaxValue),
            TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);

        return Math.Max(font.Height + 4, size.Height + 2);
    }

    private int MeasureWhitespaceWidth(string text, Font font)
    {
        string normalized = text.Replace("\t", "    ").Replace(" ", "\u00A0");
        return Math.Max(1, TextRenderer.MeasureText(normalized, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width - 2);
    }

    private int MeasureCodeHeight(string code, float fontSize)
    {
        Font font = GetCodeFont(fontSize, FontStyle.Regular);
        string normalized = NormalizeLineEndings(string.IsNullOrEmpty(code) ? " " : code.Replace("\t", "    "));
        string[] lines = normalized.Split('\n');
        int lineHeight = TextRenderer.MeasureText("Mg", font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Height;
        return Math.Max(lineHeight + 8, (lines.Length * lineHeight) + 8);
    }

    private static IEnumerable<string> SplitTextTokens(string text)
    {
        return WhitespaceRegex.Split(text);
    }

    private static FontStyle CombineStyles(FontStyle baseStyle, bool bold, bool italic)
    {
        FontStyle style = baseStyle;
        if (bold)
        {
            style |= FontStyle.Bold;
        }

        if (italic)
        {
            style |= FontStyle.Italic;
        }

        return style;
    }

    private static string FlattenSegments(IReadOnlyList<InlineSegment> segments)
    {
        StringBuilder builder = new StringBuilder();
        for (int index = 0; index < segments.Count; index++)
        {
            builder.Append(segments[index].Text);
        }

        return builder.ToString();
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static void AppendParagraphLine(StringBuilder paragraphBuilder, string line)
    {
        if (paragraphBuilder.Length > 0)
        {
            paragraphBuilder.AppendLine();
        }

        paragraphBuilder.Append(line);
    }

    private static void FlushParagraph(List<MarkdownBlock> blocks, StringBuilder paragraphBuilder)
    {
        if (paragraphBuilder.Length == 0)
        {
            return;
        }

        blocks.Add(new ParagraphBlock(paragraphBuilder.ToString()));
        paragraphBuilder.Clear();
    }

    private static void FlushList(List<MarkdownBlock> blocks, ref List<ListItemBlock>? currentListItems, ref bool currentListOrdered)
    {
        if (currentListItems is null || currentListItems.Count == 0)
        {
            currentListItems = null;
            currentListOrdered = false;
            return;
        }

        blocks.Add(new ListBlock(currentListOrdered, currentListItems));
        currentListItems = null;
        currentListOrdered = false;
    }

    private static void FlushQuote(List<MarkdownBlock> blocks, ref List<string>? quoteLines)
    {
        if (quoteLines is null || quoteLines.Count == 0)
        {
            quoteLines = null;
            return;
        }

        blocks.Add(new QuoteBlock(quoteLines));
        quoteLines = null;
    }

    private static string TrimTrailingNewLine(string text)
    {
        return text.TrimEnd('\r', '\n');
    }

    private static int GetIndentLevel(string indentation)
    {
        if (string.IsNullOrEmpty(indentation))
        {
            return 0;
        }

        int spaces = 0;
        for (int index = 0; index < indentation.Length; index++)
        {
            spaces += indentation[index] == '\t' ? 4 : 1;
        }

        return Math.Max(0, spaces / 2);
    }

    private static string ExtractQuoteContent(string line)
    {
        string content = line.TrimStart();
        if (content.StartsWith(">", StringComparison.Ordinal))
        {
            content = content.Substring(1);
        }

        return content.TrimStart();
    }

    private static string CleanLinkUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        int titleIndex = trimmed.IndexOf(' ');
        if (titleIndex >= 0)
        {
            trimmed = trimmed.Substring(0, titleIndex);
        }

        return trimmed.Trim();
    }

    private static Panel CreateSpacer(int width, int height)
    {
        return new Panel
        {
            Width = Math.Max(1, width),
            Height = Math.Max(1, height),
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.Transparent,
        };
    }

    private abstract class MarkdownBlock
    {
    }

    private sealed class HeaderBlock : MarkdownBlock
    {
        public HeaderBlock(int level, string text)
        {
            Level = level;
            Text = text;
        }

        public int Level { get; }

        public string Text { get; }
    }

    private sealed class ParagraphBlock : MarkdownBlock
    {
        public ParagraphBlock(string text)
        {
            Text = text;
        }

        public string Text { get; }
    }

    private sealed class CodeBlock : MarkdownBlock
    {
        public CodeBlock(string? language, string code)
        {
            Language = language;
            Code = code;
        }

        public string? Language { get; }

        public string Code { get; }
    }

    private sealed class ListBlock : MarkdownBlock
    {
        public ListBlock(bool ordered, List<ListItemBlock> items)
        {
            Ordered = ordered;
            Items = items;
        }

        public bool Ordered { get; }

        public List<ListItemBlock> Items { get; }
    }

    private sealed class HorizontalRuleBlock : MarkdownBlock
    {
        public static HorizontalRuleBlock Instance { get; } = new HorizontalRuleBlock();

        private HorizontalRuleBlock()
        {
        }
    }

    private sealed class QuoteBlock : MarkdownBlock
    {
        public QuoteBlock(List<string> lines)
        {
            Lines = lines;
        }

        public List<string> Lines { get; }
    }

    private sealed class ListItemBlock
    {
        public ListItemBlock(string marker, string text, int indentLevel)
        {
            Marker = marker;
            Text = text;
            IndentLevel = indentLevel;
        }

        public string Marker { get; }

        public string Text { get; }

        public int IndentLevel { get; }
    }

    private sealed class InlineSegment
    {
        public InlineSegment(string text, bool bold, bool italic, bool isCode, string? linkUrl)
        {
            Text = text;
            Bold = bold;
            Italic = italic;
            IsCode = isCode;
            LinkUrl = linkUrl;
        }

        public string Text { get; }

        public bool Bold { get; }

        public bool Italic { get; }

        public bool IsCode { get; }

        public string? LinkUrl { get; }

        public bool IsLink => !string.IsNullOrWhiteSpace(LinkUrl);
    }

    private sealed class RenderState
    {
        public StringBuilder Markdown { get; } = new StringBuilder();

        public FlowLayoutPanel? Host { get; set; }

        public bool IsRendering { get; set; }
    }

    private sealed class RoundedPanel : Panel
    {
        private readonly int radius;

        public RoundedPanel(int cornerRadius)
        {
            radius = cornerRadius;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width, Height);
            using (GraphicsPath path = CreateRoundedPath(rect, radius))
            using (SolidBrush brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillPath(brush, path);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRegion();
        }

        private void ApplyRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            Rectangle rect = new Rectangle(0, 0, Width, Height);
            using (GraphicsPath path = CreateRoundedPath(rect, radius))
            {
                Region = new Region(path);
            }
        }

        private static GraphicsPath CreateRoundedPath(Rectangle rect, int r)
        {
            int d = r * 2;
            GraphicsPath path = new GraphicsPath();
            path.StartFigure();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
