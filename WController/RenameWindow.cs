using System;
using System.Windows.Forms;

namespace WController;

public partial class RenameWindow : Form
{
    public bool IsOriginalSuggestName { get; set; } = false;
    public string SuggestName { get; set; } = string.Empty;
    public string SuggestShortcut { get; set; } = string.Empty;
    public string NewName => textBox.Text;
    public string Shortcut => textBoxShortcut.Text.ToUpper();
    public RenameWindow()
    {
        InitializeComponent();

        this.KeyPreview = true;
        this.KeyDown += this.OnKeyDown;
        this.Load += this.OnLoad;
        this.FormClosing += this.OnFormClosing;
        textBox.TextChanged += this.OnTextChanged;
    }
    private void OnLoad(object sender, EventArgs e)
    {
        textBox.Text = SuggestName;
        textBoxShortcut.Text = SuggestShortcut;
    }

    private void OnFormClosing(object sender, FormClosingEventArgs e)
    {
        if (SuggestName == NewName && IsOriginalSuggestName)
        {
            textBox.Text = string.Empty;
        }
    }

    private void OnTextChanged(object sender, EventArgs e)
    {
        if (textBox.Text == SuggestName && IsOriginalSuggestName)
        {
            buttonOk.Text = "Keep name";
        }
        else if (textBox.Text.Length > 0)
        {
            buttonOk.Text = "Apply";
        }
        else
        {
            buttonOk.Text = "Remove name";
        }
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
}
