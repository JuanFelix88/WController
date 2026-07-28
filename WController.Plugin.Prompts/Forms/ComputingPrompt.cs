using System.Drawing;
using System.Windows.Forms;

namespace WController.Plugin.Prompts.Forms;
public partial class ComputingPrompt : Form
{
    public static ComputingPrompt ShowWithDescription(string description)
    {
        ComputingPrompt prompt = new ComputingPrompt();
        prompt.StartPosition = FormStartPosition.Manual;
        prompt.Location = new Point(Screen.PrimaryScreen.WorkingArea.Width - prompt.Width - 5, Screen.PrimaryScreen.WorkingArea.Height - prompt.Height - 4);
        prompt.ShowInTaskbar = false;
        prompt.TopMost = true;

        prompt.label1.Text = description.Length > 50 ? description.Substring(0, 50) + "..." : description;
        prompt.Show();
        return prompt;

    }

    public ComputingPrompt()
    {
        InitializeComponent();
    }
}
