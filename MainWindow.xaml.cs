using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ETR
{
    public partial class MainWindow : Window
    {
        const string CONFIG_FILENAME = "lastpath.ini";

        readonly BackgroundWorker worker = new BackgroundWorker { WorkerReportsProgress = true };
        string filepath;

        public MainWindow()
        {
            InitializeComponent();
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
        }

        void Button_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                DefaultExt = ".exe",
                Filter = ".NET Module (*.exe)|*.exe"
            };

            string lastPath = ReadLastPath();
            if (!string.IsNullOrEmpty(lastPath) && Directory.Exists(lastPath))
            {
                dlg.InitialDirectory = lastPath;
            }

            if (dlg.ShowDialog() != true) return;

            filepath = dlg.FileName;
            txt_assembly.Text = filepath;
            SaveLastPath(filepath);
            worker.RunWorkerAsync();
        }

        void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            var w = (BackgroundWorker)sender;
            string outputPath = DefaultRemovedPath(filepath);
            e.Result = Patcher.Run(filepath, outputPath, (pct, msg) => w.ReportProgress(pct, msg));
        }

        void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            DeterminateCircularProgress.Value = e.ProgressPercentage;
        }

        void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            DeterminateCircularProgress.Value = 100;

            if (e.Error != null)
            {
                label_done.Text = "Failed";
                label_done.Visibility = Visibility.Visible;
                MessageBox.Show(e.Error.Message);
                return;
            }

            var result = (PatchResult)e.Result;
            if (result == PatchResult.NoTypeFound)
            {
                label_done.Text = "Failed";
                label_done.Visibility = Visibility.Visible;
                MessageBox.Show("version not support.");
            }
            else
            {
                label_done.Visibility = Visibility.Visible;
            }
        }

        static string DefaultRemovedPath(string input)
        {
            string dir = Path.GetDirectoryName(input);
            string noExt = Path.GetFileNameWithoutExtension(input);
            string ext = Path.GetExtension(input);
            return Path.Combine(dir ?? "", noExt + "-Removed" + ext);
        }

        string ReadLastPath()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CONFIG_FILENAME);
            if (!File.Exists(configPath)) return null;
            try { return File.ReadAllText(configPath); }
            catch { return null; }
        }

        void SaveLastPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            string dir = Path.GetDirectoryName(path);
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CONFIG_FILENAME);
            try { File.WriteAllText(configPath, dir); }
            catch { /* best effort */ }
        }
    }
}
