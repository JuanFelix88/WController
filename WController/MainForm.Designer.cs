namespace WController
{
    partial class MainForm
    {
        /// <summary>
        /// Variável de designer necessária.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpar os recursos que estão sendo usados.
        /// </summary>
        /// <param name="disposing">true se for necessário descartar os recursos gerenciados; caso contrário, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código gerado pelo Windows Form Designer

        /// <summary>
        /// Método necessário para suporte ao Designer - não modifique 
        /// o conteúdo deste método com o editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.listBox1 = new System.Windows.Forms.ListBox();
            this.pnLists = new System.Windows.Forms.Panel();
            this.pnPreview = new System.Windows.Forms.Panel();
            this.lbPreview = new System.Windows.Forms.Label();
            this.lbSelectedWindow = new System.Windows.Forms.Label();
            this.pnLists.SuspendLayout();
            this.pnPreview.SuspendLayout();
            this.SuspendLayout();
            // 
            // listBox1
            // 
            this.listBox1.AccessibleRole = System.Windows.Forms.AccessibleRole.MenuBar;
            this.listBox1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.listBox1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.listBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBox1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(230)))), ((int)(((byte)(230)))), ((int)(((byte)(230)))));
            this.listBox1.FormattingEnabled = true;
            this.listBox1.ItemHeight = 20;
            this.listBox1.Location = new System.Drawing.Point(0, 0);
            this.listBox1.Margin = new System.Windows.Forms.Padding(0);
            this.listBox1.Name = "listBox1";
            this.listBox1.Size = new System.Drawing.Size(437, 340);
            this.listBox1.TabIndex = 0;
            // 
            // pnLists
            // 
            this.pnLists.Controls.Add(this.listBox1);
            this.pnLists.Dock = System.Windows.Forms.DockStyle.Left;
            this.pnLists.Location = new System.Drawing.Point(8, 8);
            this.pnLists.Name = "pnLists";
            this.pnLists.Size = new System.Drawing.Size(437, 340);
            this.pnLists.TabIndex = 1;
            // 
            // pnPreview
            // 
            this.pnPreview.Controls.Add(this.lbPreview);
            this.pnPreview.Controls.Add(this.lbSelectedWindow);
            this.pnPreview.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnPreview.Location = new System.Drawing.Point(451, 8);
            this.pnPreview.Name = "pnPreview";
            this.pnPreview.Size = new System.Drawing.Size(406, 340);
            this.pnPreview.TabIndex = 0;
            // 
            // lbPreview
            // 
            this.lbPreview.AutoSize = true;
            this.lbPreview.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbPreview.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.lbPreview.Location = new System.Drawing.Point(140, 147);
            this.lbPreview.Name = "lbPreview";
            this.lbPreview.Size = new System.Drawing.Size(127, 16);
            this.lbPreview.TabIndex = 0;
            this.lbPreview.Text = "No selected window";
            // 
            // lbSelectedWindow
            // 
            this.lbSelectedWindow.AutoSize = true;
            this.lbSelectedWindow.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lbSelectedWindow.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(144)))), ((int)(((byte)(145)))), ((int)(((byte)(226)))));
            this.lbSelectedWindow.Location = new System.Drawing.Point(2, 308);
            this.lbSelectedWindow.Name = "lbSelectedWindow";
            this.lbSelectedWindow.Size = new System.Drawing.Size(16, 16);
            this.lbSelectedWindow.TabIndex = 1;
            this.lbSelectedWindow.Text = "...";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.ClientSize = new System.Drawing.Size(865, 356);
            this.ControlBox = false;
            this.Controls.Add(this.pnPreview);
            this.Controls.Add(this.pnLists);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm";
            this.Padding = new System.Windows.Forms.Padding(8);
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Windows Controller";
            this.TopMost = true;
            this.pnLists.ResumeLayout(false);
            this.pnPreview.ResumeLayout(false);
            this.pnPreview.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox listBox1;
        private System.Windows.Forms.Panel pnLists;
        private System.Windows.Forms.Panel pnPreview;
        private System.Windows.Forms.Label lbPreview;
        private System.Windows.Forms.Label lbSelectedWindow;
    }
}

