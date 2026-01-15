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
    public MainForm.WindowItem? SelectedWindow => cbPrograms.Items[cbPrograms.SelectedIndex] as MainForm.WindowItem;

    public string Shortcut => textBoxShortcut.Text.ToUpper();
    public SetAliasForm(IEnumerable<MainForm.WindowItem> suggestWndows)
    {
        InitializeComponent();

        this.KeyPreview = true;
        this.KeyDown += this.OnKeyDown;
        this.Load += this.OnLoad;

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
    private void OnLoad(object sender, EventArgs e)
    {
        textBoxShortcut.Text = SuggestShortcut;
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

        form.cbPrograms.Text = window.Title;
        form.cbPrograms.Visible = false;
        form.cbPrograms.Enabled = false;
        form.Text = $"Edit alias to {window.Title}";

        if (form.ShowDialog() == DialogResult.OK)
        {
            window.Shortcut = form.Shortcut;
            return window;
        }

        return null;
    }
}
