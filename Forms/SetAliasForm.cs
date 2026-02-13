using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace WController.Forms;

public partial class SetAliasForm : Form
{
    public bool IsOriginalSuggestName { get; set; } = false;
    public string SuggestShortcut { get; set; } = string.Empty;
    public MatchMode MatchMode => checkBoxRegex.Checked ? MatchMode.Regex : MatchMode.Path;
    public string RegexPattern => textBoxRegex.Text;
    public MainForm.WindowItem? SelectedWindow => cbPrograms.Items[cbPrograms.SelectedIndex] as MainForm.WindowItem;

    public string Shortcut => textBoxShortcut.Text.ToUpper();
    public SetAliasForm(IEnumerable<MainForm.WindowItem> suggestWndows)
    {
        InitializeComponent();

        this.KeyPreview = true;
        this.KeyDown += this.OnKeyDown;
        this.Load += this.OnLoad;

        checkBoxRegex.CheckedChanged += this.OnRegexToggled;

        cbPrograms.ItemMapper = (item) =>
        {
            if (item is not MainForm.WindowItem windowItem)
            {
                throw new Exception("Invalid item type");
            }

            return (windowItem.Icon, windowItem.Title);
        };

        suggestWndows
            .ToList()
            .ForEach(wnd => cbPrograms.Items.Add(wnd));
    }

    private void OnRegexToggled(object sender, EventArgs e)
    {
        bool isRegex = checkBoxRegex.Checked;
        textBoxRegex.Visible = isRegex;
        labelRegex.Visible = isRegex;
        cbPrograms.Visible = !isRegex && cbPrograms.Enabled;
        label1.Text = isRegex ? "Regex" : "Program";
    }
    private void OnLoad(object sender, EventArgs e)
    {
        textBoxShortcut.Text = SuggestShortcut;
        OnRegexToggled(this, EventArgs.Empty);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            this.OnOkClick(sender, e);
        }
        else if (e.KeyCode == Keys.Escape)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }

    private void OnOkClick(object sender, EventArgs e)
    {
        this.DialogResult = DialogResult.OK;
        this.Close();
    }

    public static WindowConfigurable? OpenNew(IEnumerable<MainForm.WindowItem> suggestWndows)
    {
        using SetAliasForm form = new SetAliasForm(suggestWndows);
        if (form.ShowDialog() == DialogResult.OK)
        {
            if (form.MatchMode == MatchMode.Regex)
            {
                return new WindowConfigurable
                {
                    Title = form.RegexPattern,
                    Shortcut = form.Shortcut,
                    MatchMode = MatchMode.Regex,
                    RegexPattern = form.RegexPattern,
                };
            }

            string programPath = Util.WinHelper.GetPathFrom(form.SelectedWindow);
            string softwareName = Util.WinHelper.GetSoftwareNameFromPath(programPath);
            return new WindowConfigurable
            {
                ProgramPath = programPath,
                Title = softwareName,
                Shortcut = form.Shortcut,
            };
        }
        return null;
    }

    public static WindowConfigurable? OpenEdit(WindowConfigurable window)
    {
        using SetAliasForm form = new SetAliasForm([])
        {
            IsOriginalSuggestName = !string.IsNullOrEmpty(window.Shortcut),
            SuggestShortcut = window.Shortcut
        };

        if (window.MatchMode == MatchMode.Regex)
        {
            form.checkBoxRegex.Checked = true;
            form.textBoxRegex.Text = window.RegexPattern;
        }

        form.cbPrograms.Text = window.Title;
        form.cbPrograms.Visible = window.MatchMode != MatchMode.Regex;
        form.cbPrograms.Enabled = false;
        form.Text = $"Edit alias to {window.Title}";

        if (form.ShowDialog() == DialogResult.OK)
        {
            window.Shortcut = form.Shortcut;
            window.MatchMode = form.MatchMode;
            window.RegexPattern = form.RegexPattern;
            if (form.MatchMode == MatchMode.Regex)
            {
                window.Title = form.RegexPattern;
                window.InvalidateRegex();
            }
            return window;
        }

        return null;
    }
}
