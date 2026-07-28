using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WController.Plugin.Prompts.Forms;
public partial class CompletePrompt : Form
{
    public static bool NeedToComplete(string prompt) => fieldRegex.IsMatch(prompt);
    private static Regex fieldRegex = new(@"\{\{(.*?)\}\}", RegexOptions.Compiled);
    public string OriginalPrompt = null;
    public string CompletedPrompt { get; private set; }
    public event Action<CompletePrompt> PromptCompleted;
    public CompletePrompt()
    {
        InitializeComponent();
        pnFields.Controls.Clear();

        KeyPreview = true;
        btSave.Click += OnSave;
        KeyDown += this.OnKeyDown;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
            return;
        }

        if (e.KeyCode == (Keys.Control | Keys.Enter))
        {
            OnSave(this, EventArgs.Empty);
            return;
        }
    }

    public void ShowWithPrompt(string prompt)
    {
        rtbPrompt.Text = prompt;
        OriginalPrompt = prompt;

        Fields.Field lastField = null;
        fieldRegex.Matches(prompt).Cast<Match>().ToList().ForEach(m =>
        {
            var fieldContent = m.Groups[1].Value.Split(':');
            var fieldName = fieldContent[0];
            var fieldType = fieldContent.Length > 1 ? fieldContent[1] : "string";
            var field = new Fields.Field(fieldName, fieldType);
            if (lastField is not null)
            {
                field.Top = lastField.Bottom + 1;
            }
            
            field.FieldChanged += this.OnFieldChanged;
            pnFields.Controls.Add(field);
            lastField = field;
        });
        pnFields.Controls.Cast<Fields.Field>().ToList().FirstOrDefault()?.Select();

        Show();
    }

    private void OnFieldChanged(Fields.Field field)
    {
        //Invoke(ReplaceAllTemplates);
    }

    private void ReplaceAllTemplates()
    {
        pnFields.Controls.OfType<Fields.Field>().ToList().ForEach(f =>
        {
            if (string.IsNullOrEmpty(f.Value))
                return;
            rtbPrompt.Text = rtbPrompt.Text.Replace(f.FormattedField, f.Value);
        });
    }

    private void OnSave(object sender, System.EventArgs e)
    {
        ReplaceAllTemplates();
        CompletedPrompt = rtbPrompt.Text;
        this.DialogResult = DialogResult.OK;
        this.btSave.Enabled = false;
        PromptCompleted?.Invoke(this);
    }
}
