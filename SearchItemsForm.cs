using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Drawing.Drawing2D;
using System.Collections;
using System.Windows.Forms.VisualStyles;
using WController.Properties;
using System.IO;
using WController.Util;

namespace WController
{
    public partial class SearchItemsForm : Form
    {
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams handleparam = base.CreateParams;
                handleparam.ExStyle |= 0x02000000;
                return handleparam;
            }
        }

        const int MOD_ALT = 0x1;
        const int MOD_CONTROL = 0x2;
        const int MOD_WIN = 0x8;
        const int WM_HOTKEY = 0x0312;
        const int HOTKEY_ID = 9000;

        delegate bool EnumWindowsPrc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, Keys vk);

        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public static Color GetWindowsAccentColor()
        {
            return Color.FromArgb(32, 32, 32);
        }

        public static Color GetWindowsSecondaryColor()
        {
            return Color.FromArgb(57, 57, 57);
        }

        private void ApplyRoundedRegion(int radius)
        {
            Rectangle bounds = this.ClientRectangle;
            using (GraphicsPath path = GetRoundedPath(bounds, radius))
            {
                this.Region = new Region(path);
            }
        }


        private static Color GetFixedSolidColor(Color c)
        {
            var alphaDiff = 255 - c.A;
            float percent = (((float)c.A) * 1) / 255;

            int r = (int)Math.Floor(percent * c.R);
            int g = (int)Math.Floor(percent * c.G);
            int b = (int)Math.Floor(percent * c.B);

            if (r < 0) r = 0;
            if (g < 0) g = 0;
            if (b < 0) b = 0;

            return Color.FromArgb(r, g, b);
        }

        private static Color IncrementColor(Color c, float percent = 1.1f)
        {
            int r = (int)Math.Floor(percent * c.R);
            int g = (int)Math.Floor(percent * c.G);
            int b = (int)Math.Floor(percent * c.B);

            if (r > 255) r = 255;
            if (g > 255) g = 255;
            if (b > 255) b = 255;

            return Color.FromArgb(r, g, b);
        }

        private readonly Color AccentColor;
        private readonly Color SecondaryColor;
        private readonly Color TertiaryColor = Color.FromArgb(30, 76, 114);
        private readonly FileIndexes fileIndexes = new FileIndexes();

        public SearchItemsForm()
        {
            AccentColor = GetWindowsAccentColor();
            SecondaryColor = GetWindowsSecondaryColor();
            InitializeComponent();
            this.Hide();
            var accentColor = GetFixedSolidColor(AccentColor);

            textBox.KeyDown += this.OnTextBoxKeyDown;

            this.BackColor = accentColor;
            this.listBox1.BackColor = accentColor;
            listBox1.DrawMode = DrawMode.OwnerDrawFixed;
            this.listBox1.DrawItem += this.listBox1_DrawItem;


            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_ALT, Keys.Space);

            this.Resize += this.OnResize;
            this.Load += this.OnLoad;
            this.Paint += this.OnPaint;
            this.Deactivate += this.OnDeactivate;
            this.VisibleChanged += this.OnVisibleChanged;
            
            listBox1.DoubleClick += (s, e) =>
            {
                ((FileIndexed)listBox1.Items[listBox1.SelectedIndex]).Open();
            };

            listBox1.KeyDown += this.OnListBoxKeyDown;
            ComputeHeightSize();
        }

        private void OnVisibleChanged(object sender, EventArgs e)
        {
            if (!this.Visible)
            {
                listBox1.Items.Clear();
            }
        }

        private void SearchWithText(string text)
        {
            var shortcuts = fileIndexes.SearchFiles(text);

            listBox1.SuspendLayout();
            listBox1.Items.Clear();
            foreach (var shortcut in shortcuts)
            {
                listBox1.Items.Add(shortcut);
            }

            listBox1.Height = (listBox1.Items.Count * 24)+ 5;

            if (shortcuts.Any())
            {
                listBox1.SelectedIndex = 0;
            }

            listBox1.ResumeLayout();
            ComputeHeightSize();
            ApplyRoundedRegion(20);
            Refresh();
        }

        private async void OnTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up)
            {
                e.SuppressKeyPress = true;
                SelectPrevItem(true);
                return;
            }

            if (e.KeyCode == Keys.Down)
            {
                e.SuppressKeyPress = true;
                SelectNextItem(true);
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                this.Hide();
                return;
            }

            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ((FileIndexed)listBox1.Items[listBox1.SelectedIndex]).Open();
                this.Hide();
                return;
            }

            if (e.KeyCode == Keys.Back && e.Control)
            {
                this.textBox.Text = string.Empty;
                e.SuppressKeyPress = true;
            }

            await Task.Delay(10);
            SearchWithText(textBox.Text);
            label1.Visible = textBox.Text.Length == 0;
        }

        private void OnDeactivate(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void OnListBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && e.Shift)
            {
                e.Handled = true;
                MessageBox.Show("Program closed.", this.Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Process.GetCurrentProcess().Kill();
                return;
            }
            if (e.KeyCode == Keys.Escape)
            {
                this.Hide();
                return;
            }
            if (e.KeyCode == Keys.Enter && listBox1.SelectedItem is FileIndexed item)
            {
                item.Open();
                this.Hide();
                return;
            }
        }


        private void ComputeHeightSize()
        {
            int newSize = listBox1.Items.Count * 24;
            newSize += 27;
            newSize += 15;

            if (newSize < 120) newSize = 120;

            Action func = () =>
            {
                this.Height = newSize;
            };

            if (this.InvokeRequired) this.Invoke(func);
            else func.Invoke();
        }

        private void listBox1_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            ListBox lb = (ListBox)sender;
            string text = lb.Items[e.Index].ToString();
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            Color backColor = selected ? TertiaryColor : lb.BackColor;
            Color foreColor = selected ? Color.White : lb.ForeColor;

            e.Graphics.FillRectangle(new SolidBrush(backColor), e.Bounds);

            int iconSize = e.Bounds.Height - 4;
            Image icon = ((FileIndexed)lb.Items[e.Index]).Image;

            Rectangle iconRect = new Rectangle(e.Bounds.X + 2, e.Bounds.Y + 2, iconSize, iconSize);

            if (icon != null)
                e.Graphics.DrawImage(icon, iconRect);

            int textX = iconRect.Right + 4;

            Rectangle textRect = new Rectangle(textX, e.Bounds.Y, e.Bounds.Width - textX, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, text, lb.Font, textRect, foreColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            if (selected)
            {
                using (Pen borderPen = new Pen(IncrementColor(TertiaryColor, 2f), 1.5f))
                {
                    borderPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
                    Rectangle borderRect = new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 1, e.Bounds.Height - 1);
                    e.Graphics.DrawRectangle(borderPen, borderRect);
                }
            }
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            int borderRadius = 20;
            int borderThickness = 2;
            Color borderColor = SecondaryColor;

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 4);

            using (GraphicsPath path = GetRoundedPath(rect, borderRadius))
            using (Pen pen = new Pen(borderColor, borderThickness))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.DrawPath(pen, path);
            }
        }

        private GraphicsPath GetRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 1;
            path.StartFigure();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void OnLoad(object sender, EventArgs e)
        {
            ApplyRoundedRegion(20);
        }

        private void OnResize(object sender, EventArgs e)
        {
            ApplyRoundedRegion(20);
        }

        private void SelectPrevItem(bool preventWrap = false)
        {
            if (listBox1.SelectedIndex != 0)
            {
                listBox1.SelectedIndex--;
            }
            else if (listBox1.Items.Count > 0 && !preventWrap)
            {
                listBox1.SelectedIndex = listBox1.Items.Count - 1;
            }
        }

        private void SelectNextItem(bool preventWrap = false)
        {
            if (listBox1.SelectedIndex < listBox1.Items.Count - 1)
            {
                listBox1.SelectedIndex++;
            }
            else if (listBox1.Items.Count > 0 && !preventWrap)
            {
                listBox1.SelectedIndex = 0;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                if (this.Visible)
                {
                    SelectNextItem();
                    this.Activate();
                }
                else
                {
                    this.WindowState = FormWindowState.Normal;
                    this.PerformLayout();
                    if (listBox1.Items.Count > 0) this.listBox1.SelectedIndex = 0;
                    this.Show();
                    this.Activate();
                    this.BringToFront();
                    this.Refresh();
                    this.textBox.Text = string.Empty;
                    label1.Visible = textBox.Text.Length == 0;
                    this.textBox.Focus();
                }

            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            base.OnFormClosing(e);
        }
    }
}
