using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WindowsController
{
    public class FileIndexes
    {
        private List<FileIndexed> files = new List<FileIndexed>();
        private System.Timers.Timer timerRefresh = new System.Timers.Timer();

        public FileIndexes(int secondsToRefresh = 60 * 5)
        {
            timerRefresh.AutoReset = true;
            timerRefresh.Enabled = true;
            timerRefresh.Interval = secondsToRefresh * 1000;
            timerRefresh.Elapsed += this.OnTimerElapsed;

            Task.Run(ComputeIndexes);
        }

        public IEnumerable<FileIndexed> SearchFiles(string text, int take = 5)
        {
            if (string.IsNullOrEmpty(text))
                return new FileIndexed[0];

            text = Util.Text.RemoveDiacritics(text).ToLowerInvariant();

            var filteredFiles = files.Where(f => f.Name.ToLowerInvariant().Contains(text)).Take(100);

            filteredFiles = filteredFiles.OrderBy(filteredFile =>
            {
                int index = filteredFile.SearchText.IndexOf(text, StringComparison.OrdinalIgnoreCase);
                return index == -1 ? int.MaxValue : index;
            });

            return filteredFiles.Take(take);
        }

        private void OnTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            ComputeIndexes();
        }

        private void GetListFromPath(string path, List<string> files)
        {
            try
            {
                foreach (string file in Directory.GetFiles(path, "*.lnk", SearchOption.TopDirectoryOnly))
                {
                    files.Add(file);
                }
            }
            catch { }

            foreach (string folder in Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly))
            {
                try { GetListFromPath(Path.Combine(path, folder), files); } catch { }
            }
        }

        private void ComputeIndexes()
        {
            string startMenuPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "Microsoft", 
                "Windows", 
                "Start Menu");

            List<string> shortcuts = new List<string>();

            GetListFromPath(startMenuPath, shortcuts);
            GetListFromPath("C:\\ProgramData\\Microsoft\\Windows\\Start Menu\\Programs", shortcuts);

            foreach(var file in files)
            {
                file.Image.Dispose(); // Dispose of the old images to free resources
            }

            files.Clear();
            foreach(var shortcut in shortcuts) {
                try
                {
                    //Util.IconHelper.GetIcon(shortcut);
                    Image image = Util.IconHelper.GetIcon(shortcut, true).ToBitmap();
                    files.Add(new FileIndexed 
                    { 
                        FullPath = shortcut, 
                        Name = Path.GetFileName(shortcut).Replace(".lnk", ""),
                        Image = Util.Resizer.ResizeImage(image, Util.Resizer.DefaultIconViewSize, Util.Resizer.DefaultIconViewSize),
                        SearchText = Util.Text.RemoveDiacritics(Path.GetFileName(shortcut).ToLowerInvariant()) + " " +
                                      Util.Text.RemoveDiacritics(Path.GetFileNameWithoutExtension(shortcut).ToLowerInvariant()) + " " +
                                      Util.Text.RemoveDiacritics(Path.GetDirectoryName(shortcut).ToLowerInvariant())
                    });
                    image.Dispose();
                }
                catch {}
            }
        }
    }

    public class FileIndexed
    {
        public string FullPath { get; set; }
        public string Name { get; set; }
        public Image Image { get; set; }
        public string SearchText { get; set; }
        public void Open()
        {
            System.Diagnostics.Process.Start("explorer.exe", FullPath);
        }
        public override string ToString() => Name;
    }
}
