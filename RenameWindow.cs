using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WController
{
    public partial class RenameWindow : Form
    {
        public string SuggestName { get; set; } = string.Empty;
        public string NewName => textBox.Text;
        public RenameWindow()
        {
            InitializeComponent();
            textBox.Text = SuggestName;

            this.KeyPreview = true;
            this.KeyDown += this.OnKeyDown;
            this.Load += this.OnLoad;
            textBox.TextChanged += this.OnTextChanged;
        }

        private void OnTextChanged(object sender, EventArgs e)
        {
            if (textBox.Text.Length > 0)
            {
                buttonOk.Text = "Apply";
            }
            else
            {
                buttonOk.Text = "Remove name";
            }
        }

        private void OnLoad(object sender, EventArgs e)
        {
            textBox.Text = SuggestName;
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
}
