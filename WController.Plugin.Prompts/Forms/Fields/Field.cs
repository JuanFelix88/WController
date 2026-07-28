using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WController.Plugin.Prompts.Forms.Fields;
public partial class Field : UserControl
{
    public string FieldType = null;
    public string FieldName { get; private set; }
    public string Value => textBox1.Text;
    public string FormattedField => $"{{{{{FieldName}:{FieldType}}}}}";
    public event Action<Field> FieldChanged;

    public Field()
    {
        InitializeComponent();
        this.textBox1.Text = "";
        FieldName ??= "Field";
        FieldType ??= "string";

        textBox1.TextChanged += this.OnTextChanged;
    }
    public Field(string name, string type) : this()
    {
        this.Name = $"f{name}";
        label1.Text = name;
        FieldName = name;
        FieldType = type;
    }

    private void OnTextChanged(object sender, EventArgs e)
    {
        FieldChanged?.Invoke(this);
    }
}
