namespace WController.Plugin.Prompts.Forms;

partial class CompletePrompt
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CompletePrompt));
            this.label1 = new System.Windows.Forms.Label();
            this.rtbPrompt = new System.Windows.Forms.RichTextBox();
            this.pnFields = new System.Windows.Forms.Panel();
            this.btSave = new System.Windows.Forms.Button();
            this.field3 = new WController.Plugin.Prompts.Forms.Fields.Field();
            this.field2 = new WController.Plugin.Prompts.Forms.Fields.Field();
            this.field1 = new WController.Plugin.Prompts.Forms.Fields.Field();
            this.pnFields.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(210)))), ((int)(((byte)(210)))), ((int)(((byte)(210)))));
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(61, 18);
            this.label1.TabIndex = 0;
            this.label1.Text = "Prompt:";
            // 
            // rtbPrompt
            // 
            this.rtbPrompt.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.rtbPrompt.ForeColor = System.Drawing.SystemColors.Info;
            this.rtbPrompt.Location = new System.Drawing.Point(15, 30);
            this.rtbPrompt.Name = "rtbPrompt";
            this.rtbPrompt.Size = new System.Drawing.Size(649, 168);
            this.rtbPrompt.TabIndex = 1;
            this.rtbPrompt.Text = "Initial text.";
            // 
            // pnFields
            // 
            this.pnFields.AutoScroll = true;
            this.pnFields.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(45)))));
            this.pnFields.Controls.Add(this.field3);
            this.pnFields.Controls.Add(this.field2);
            this.pnFields.Controls.Add(this.field1);
            this.pnFields.Location = new System.Drawing.Point(15, 217);
            this.pnFields.Name = "pnFields";
            this.pnFields.Size = new System.Drawing.Size(649, 205);
            this.pnFields.TabIndex = 4;
            // 
            // btSave
            // 
            this.btSave.BackColor = System.Drawing.Color.Gray;
            this.btSave.FlatAppearance.BorderSize = 0;
            this.btSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btSave.ForeColor = System.Drawing.Color.White;
            this.btSave.Location = new System.Drawing.Point(15, 433);
            this.btSave.Name = "btSave";
            this.btSave.Size = new System.Drawing.Size(93, 23);
            this.btSave.TabIndex = 100;
            this.btSave.Text = "Go!";
            this.btSave.UseVisualStyleBackColor = false;
            // 
            // field3
            // 
            this.field3.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(45)))));
            this.field3.Dock = System.Windows.Forms.DockStyle.Top;
            this.field3.Location = new System.Drawing.Point(0, 108);
            this.field3.Name = "field3";
            this.field3.Size = new System.Drawing.Size(649, 54);
            this.field3.TabIndex = 4;
            // 
            // field2
            // 
            this.field2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(45)))));
            this.field2.Dock = System.Windows.Forms.DockStyle.Top;
            this.field2.Location = new System.Drawing.Point(0, 54);
            this.field2.Name = "field2";
            this.field2.Size = new System.Drawing.Size(649, 54);
            this.field2.TabIndex = 3;
            // 
            // field1
            // 
            this.field1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(45)))));
            this.field1.Dock = System.Windows.Forms.DockStyle.Top;
            this.field1.Location = new System.Drawing.Point(0, 0);
            this.field1.Name = "field1";
            this.field1.Size = new System.Drawing.Size(649, 54);
            this.field1.TabIndex = 2;
            // 
            // CompletePrompt
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(45)))));
            this.ClientSize = new System.Drawing.Size(676, 478);
            this.Controls.Add(this.btSave);
            this.Controls.Add(this.pnFields);
            this.Controls.Add(this.rtbPrompt);
            this.Controls.Add(this.label1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "CompletePrompt";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Complete Prompt";
            this.pnFields.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.RichTextBox rtbPrompt;
    private Fields.Field field1;
    private Fields.Field field2;
    private System.Windows.Forms.Panel pnFields;
    private Fields.Field field3;
    private System.Windows.Forms.Button btSave;
}