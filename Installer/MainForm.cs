using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Installer
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void ManualExecutableSearch()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Find Yhx4x2 executable";
                dialog.InitialDirectory = Environment.CurrentDirectory;
                dialog.FileName = "Yhx4x2.exe";
                dialog.Filter = "Executable files (*.exe)|*.exe";

                var dialogResult = dialog.ShowDialog();

                if (dialogResult == DialogResult.OK)
                {
                    textBoxPath.Text = dialog.FileName;
                }
            }

            BringToFront();
        }

        [DllImport("shell32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsUserAnAdmin();

        private void MainForm_Load(object sender, EventArgs e)
        {
            if (!IsUserAnAdmin())
            {
                var startInfo = new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location)
                {
                    Verb = "runas",
                    UseShellExecute = true
                };

                var process = new Process
                {
                    StartInfo = startInfo
                };

                try
                {
                    process.Start();
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    MessageBox.Show("Please note that you will not be able to register the Yhx4x2 protocol without administrator privileges.", "Missing privileges", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    goto continue_exec;
                }

                Close();
                return;
            }

            continue_exec:
            // search for the actual binary in current directory
            if (File.Exists("Yhx4x2.exe"))
            {
                textBoxPath.Text = Path.GetFullPath("Yhx4x2.exe");
            }
            else
            {
                var result = MessageBox.Show("Could not find Yhx4x2 executable in current directory. Would you like to search for it manually?", "Main executable not found", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    ManualExecutableSearch();
                }
            }
        }

        private void ButtonPath_Click(object sender, EventArgs e)
        {
            ManualExecutableSearch();
        }

        private void ButtonRegister_Click(object sender, EventArgs e)
        {
            if (File.Exists(textBoxPath.Text))
            {
                Yhx4Protocol.Register(textBoxPath.Text);
                Close();
            }
            else
            {
                MessageBox.Show("You must specify a path to the main Yhx4x2 executable!", "Main executable not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ButtonUnregister_Click(object sender, EventArgs e)
        {
            Yhx4Protocol.Unregister();
        }
    }
}
