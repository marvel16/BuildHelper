using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.Serialization;
using System.IO;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.Net;
using System.ComponentModel;
using Microsoft.TeamFoundation;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using System.Management;
using Xceed.Wpf.Toolkit;
using System.Text.RegularExpressions;

namespace BuildHelper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private bool bBuildsLaunched = false;
        private CfgMan config = new CfgMan();
        private List<CheckBox> checkboxes = new List<CheckBox>();
        private Queue<Process> BuildQueue = new Queue<Process>();
        private object locker = new object();
        DispatcherTimer timer = new DispatcherTimer();
        DispatcherTimer ScheduleTimer = new DispatcherTimer();

        DateTime startTime;
        DateTime projstarttime;
        ProgressDialogController controller;
        private static long ItemCount = 0;

        public MainWindow( )
        {
            InitializeComponent();
            config.LoadConfig();

            foreach ( var item in config.Prjcfg )
                ProjectListBox.Items.Add(item);
            //initialize field from config
            tfs_username_textbox.Text = config.Tfscfg.UserName;
            tfs_path_textbox.Text = config.Tfscfg.TfsPath;
            tfs_workspace_textbox.Text = config.Tfscfg.TfsWorkspace;
            pw_passwordbox.Password = config.Tfscfg.PassWord;
            requestpath_textbox.Text = config.Tfscfg.RequestPath;

            //Tag radioButtons with resolve confict option
            noautroresolve_rbx.Tag = GetOptions.NoAutoResolve;
            none_rbx.Tag = GetOptions.None;
            overwrite_rbx.Tag = GetOptions.Overwrite;

            m_timepicker.Value = DateTime.Now;
            timer.Interval = new TimeSpan(0, 0, 1);
            timer.Tick += dispatcherTimer_Tick;
            ScheduleTimer.Tick += Scheduletimer_Tick;

        }

        private void dispatcherTimer_Tick( object sender, EventArgs e )
        {
            // Updating the Label which displays the current second
            timer_label.Content = ( DateTime.Now - startTime ).ToString(@"hh':'mm':'ss");
        }

        private void LaunchButton_OnClick( object sender, EventArgs e )
        {
            if ( config.Prjcfg.Count == 0 )
            {
                System.Windows.MessageBox.Show("Config is empty");
                return;
            }
            status_progressRing.IsActive = !status_progressRing.IsActive;

            if ( bBuildsLaunched )
            {
                StopBuild();
                return;
            }
            startTime = DateTime.Now;
            if ( ScheduleTimer.IsEnabled == false )
                output_listbox.Items.Clear();
            Launch.Content = "Cancel builds";
            timer.Start();
            CreateBuildQueue();
            StartBuild();
        }

        private void CreateBuildQueue( )
        {
            foreach ( var elem in ProjectListBox.Items )
            {
                Project proj = elem as Project;
                List<string> rebuildInfo = proj.GetRebuildInfoList();
                foreach ( var arg in rebuildInfo )
                {
                    Process process = new Process();
                    process.StartInfo.FileName = "C:/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/devenv.com";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.Arguments = "\"" + proj.ProjectPath + "\"" + @" /REBUILD " + arg;
                    process.StartInfo.CreateNoWindow = true;
                    process.EnableRaisingEvents = true;
                    process.Exited += ProcExited;
                    BuildQueue.Enqueue(process);
                }
            }
        }

        private void StartBuild( )
        {
            bBuildsLaunched = true;

            output_listbox.Items.Add("BUILDS LAUNCHED!");

            try
            {
                BuildQueue.Peek().Start();
                projstarttime = DateTime.Now;
                Task.Run(( ) => ReadOutput(BuildQueue.Peek()));
            }
            catch ( Exception ex )
            {
                System.Windows.MessageBox.Show("Failed to launch build:" + ex.Message);
            }
        }

        private void StopBuild( )
        {
            bBuildsLaunched = false;
            if ( BuildQueue.Count > 0 )
                KillProcessAndChildren(BuildQueue.Peek().Id);
            BuildQueue.Clear();
            timer.Stop();
            ScheduleTimer.Stop();
            timer_label.Content = "";
            output_listbox.Items.Add("BUILDS CANCELLED!");
            Launch.Content = "Launch builds!";

        }

        private static void KillProcessAndChildren( int pid )
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher
              ("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach ( ManagementObject mo in moc )
            {
                KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
            }
            try
            {
                Process proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch ( ArgumentException )
            {
                // Process already exited.
            }
        }

        private void ReadOutput( Process proc )
        {
            string result;
            while ( ( result = proc.StandardOutput.ReadLine() ) != null )
            {
                output_listbox.Dispatcher.Invoke(
                    delegate
                    {
                        bool isWarningErrorSuccess = false;
                        ListViewItem li = new ListViewItem();
                        li.Content = result;
                        Logger.Log(result);
                        if ( Regex.Matches(result, @"\b[Ss]ucceeded\b").Count > 0 )
                        {
                            isWarningErrorSuccess = true;
                            li.Foreground = Brushes.Green;
                        }
                        if ( Regex.Matches(result, @"\b[Ww]arning\b").Count > 0 )
                        {
                            isWarningErrorSuccess = true;
                            li.Foreground = Brushes.Orange;
                        }
                        if ( Regex.Matches(result, @"\b[Ee]rror:\b").Count > 0 || Regex.Matches(result, @"[1-9] \bfailed\b").Count > 0 || 
							Regex.Matches(result, @"\bnot found\b").Count > 0 || Regex.Matches(result, @"\b[Uu]nresolved\b").Count > 0 )
                        {
                            isWarningErrorSuccess = true;
                            li.Foreground = Brushes.Red;
                        }
                        if (isWarningErrorSuccess)
                        {
                            output_listbox.Items.Add(li);
                            output_listbox.ScrollIntoView(li);
                        }
                    });
            }
        }

        private void ProcExited( object sender, EventArgs e )
        {
            string projName = ( sender as Process ).StartInfo.Arguments;
            TimeSpan buildtime = DateTime.Now - projstarttime;
            Task.Run(( ) => AddBuildTime(projName, buildtime.Ticks));
            if ( bBuildsLaunched )
                this.Dispatcher.Invoke(( ) =>
                {
                    output_listbox.Items.Add(projName + ": " + buildtime.ToString(@"hh':'mm':'ss"));
                    //make sure no children processes alive
                    KillProcessAndChildren(BuildQueue.Peek().Id);
                    BuildQueue.Dequeue();
                    if ( BuildQueue.Count == 0 )
                    {
                        timer.Stop();
                        Launch.Content = "Launch builds!";
                        status_progressRing.IsActive = !status_progressRing.IsActive;
                        bBuildsLaunched = false;
                        Launch.IsEnabled = true;
                    }
                    else
                        StartBuild();
                });
        }

        private void AddBuildTime( string arg, long time )
        {
            foreach ( var proj in config.Prjcfg )
            {
                if ( arg.Contains(proj.ProjectPath) )
                {
                    proj.buildTimes.Add(time);
                    config.SaveConfig();
                    return;
                }
            }
        }

        private GetOptions GetFetchOptions( )
        {
            var rbx = Src_grid.Children.OfType<RadioButton>().FirstOrDefault(r => (bool)r.IsChecked);
            return (GetOptions)rbx.Tag;
        }

        private void FetchCode( string userName, string userPass, string tfsPath, string tfsWorkSpace, string requestPath, GetOptions opts )
        {
            GetStatus getStat = null;
            try
            {
                ICredentials myCred = new NetworkCredential(userName, userPass);
                TfsTeamProjectCollection tfs = new TfsTeamProjectCollection(new Uri(tfsPath), myCred);
                tfs.EnsureAuthenticated();
                VersionControlServer vcs = tfs.GetService<VersionControlServer>();
                Workspace myWorkspace = vcs.GetWorkspace(tfsWorkSpace, vcs.AuthorizedUser);
                vcs.Getting += OnGettingEvent;
                ItemCount = 0;// reset server items counter
                GetRequest request = new GetRequest(new ItemSpec(requestPath, RecursionType.Full), VersionSpec.Latest);

                getStat = myWorkspace.Get(request, opts);
            }
            catch ( Exception ex )
            {
                output_listbox.Dispatcher.Invoke((Action)( ( ) =>
                {
                    output_listbox.Items.Add("Fetching code failed with exception: " + ex.Message);
                } ));
                return;
            }
            if ( getStat == null || getStat.NumFailures > 0 || getStat.NumWarnings > 0 )
            {
                output_listbox.Dispatcher.Invoke((Action)( ( ) =>
                {
                    output_listbox.Items.Add("Errors while getting latest have occurred");
                } ));
                return;
            }

            if ( getStat.NumOperations == 0 )
            {
                output_listbox.Dispatcher.Invoke((Action)( ( ) =>
                {
                    output_listbox.Items.Add("All files are up to date");
                } ));
            }
            else
                output_listbox.Dispatcher.Invoke((Action)( ( ) =>
                {
                    output_listbox.Items.Add("Successfully downloaded code");
                } ));
        }

        private void x64R_checkbox_CheckedChange( object sender, RoutedEventArgs e )
        {
            if ( ProjectListBox.SelectedIndex < 0 )
                return;
            config.Prjcfg[ProjectListBox.SelectedIndex].x64R = (bool)( (CheckBox)sender ).IsChecked;
            config.SaveConfig();
        }

        private void x64D_checkbox_CheckedChange( object sender, RoutedEventArgs e )
        {
            if ( ProjectListBox.SelectedIndex < 0 )
                return;
            config.Prjcfg[ProjectListBox.SelectedIndex].x64D = (bool)( (CheckBox)sender ).IsChecked;
            config.SaveConfig();
        }

        private void x86R_checkbox_CheckedChange( object sender, RoutedEventArgs e )
        {
            if ( ProjectListBox.SelectedIndex < 0 )
                return;
            config.Prjcfg[ProjectListBox.SelectedIndex].x86R = (bool)( (CheckBox)sender ).IsChecked;
            config.SaveConfig();
        }

        private void x86D_checkbox_CheckedChange( object sender, RoutedEventArgs e )
        {
            if ( ProjectListBox.SelectedIndex < 0 )
                return;
            config.Prjcfg[ProjectListBox.SelectedIndex].x86D = (bool)( (CheckBox)sender ).IsChecked;
            config.SaveConfig();
        }

        private void ProjectListBox_SelectionChanged( object sender, SelectionChangedEventArgs e )
        {
            if ( ProjectListBox.SelectedIndex < 0 )
                return;
            x64D_checkbox1.IsChecked = ( ProjectListBox.Items[ProjectListBox.SelectedIndex] as Project ).x64D;
            x64R_checkbox1.IsChecked = ( ProjectListBox.Items[ProjectListBox.SelectedIndex] as Project ).x64R;
            x86R_checkbox1.IsChecked = ( ProjectListBox.Items[ProjectListBox.SelectedIndex] as Project ).x86R;
            x86D_checkbox1.IsChecked = ( ProjectListBox.Items[ProjectListBox.SelectedIndex] as Project ).x86D;
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            output_listbox.Items.Clear();
        }

        private void createProject_button_Click( object sender, RoutedEventArgs e )
        {
            if ( String.IsNullOrEmpty(Projectname_textbox.Text) )
            {
                System.Windows.MessageBox.Show("Project name is empty");
                return;
            }
            if ( !File.Exists(Projectpath_textbox.Text) )
            {
                System.Windows.MessageBox.Show("Project path not found");
                return;
            }

            var proj = new Project();
            proj.ProjectName = Projectname_textbox.Text;
            proj.ProjectPath = Projectpath_textbox.Text;
            ProjectListBox.Items.Add(proj);
            config.Prjcfg.Add(proj);
            config.SaveConfig();
            Projectname_textbox.Clear();
            Projectpath_textbox.Clear();
        }

        private void removeproject_button_Click( object sender, RoutedEventArgs e )
        {
            if ( ProjectListBox.SelectedIndex < 0 )
                return;
            config.Prjcfg.RemoveAt(ProjectListBox.SelectedIndex);
            ProjectListBox.Items.Clear();
            foreach ( var item in config.Prjcfg )
                ProjectListBox.Items.Add(item);
            config.SaveConfig();
        }

        private async void FetchButton_OnClick( object sender, RoutedEventArgs e )
        {
            Launch.IsEnabled = false;
            string userName = tfs_username_textbox.Text;
            string userPass = pw_passwordbox.Password;
            string tfsPath = tfs_path_textbox.Text;
            string tfsWorkSpace = tfs_workspace_textbox.Text;
            string requestPath = requestpath_textbox.Text;

            controller = await this.ShowProgressAsync("Please wait", "Downloading...", true);
            GetOptions opts = GetFetchOptions();
            await Task.Run(( ) => FetchCode(userName, userPass, tfsPath, tfsWorkSpace, requestPath, opts));
            Launch.IsEnabled = true;
            await controller.CloseAsync();

        }

        private void OnGettingEvent( object sender, GettingEventArgs e )
        {
            if ( e.Total == 0 )
                return;
            ItemCount++;
            double progress = ItemCount / (double)e.Total;
            this.Dispatcher.Invoke(( ) =>
            {
                if ( progress > 1 )
                    controller.SetProgress((int)progress);
                else
                    controller.SetProgress(progress);
                controller.SetMessage("Downloading...\n" + ItemCount.ToString() + " of "+ e.Total.ToString() + ": "+ e.ServerItem);
            });
        }

        private void FetchCheckBox_Click( object sender, RoutedEventArgs e )
        {

        }

        private void rememberTFScfg_click( object sender, RoutedEventArgs e )
        {
            config.Tfscfg.UserName = tfs_username_textbox.Text;
            config.Tfscfg.TfsPath = tfs_path_textbox.Text;
            config.Tfscfg.TfsWorkspace = tfs_workspace_textbox.Text;
            config.Tfscfg.RequestPath = requestpath_textbox.Text;
            config.Tfscfg.PassWord = pw_passwordbox.Password;
            config.SaveConfig();
        }


        private void On_moveup( object sender, RoutedEventArgs e )
        {
            MoveItem(-1);
        }

        private void On_movedown( object sender, RoutedEventArgs e )
        {
            MoveItem(1);
        }

        public void MoveItem( int direction )
        {
            // Checking selected item
            if ( ProjectListBox.SelectedItem == null || ProjectListBox.SelectedIndex < 0 )
                return; // No selected item - nothing to do

            // Calculate new index using move direction
            int newIndex = ProjectListBox.SelectedIndex + direction;

            // Checking bounds of the range
            if ( newIndex < 0 || newIndex >= ProjectListBox.Items.Count )
                return; // Index out of range - nothing to do

            object selected = ProjectListBox.SelectedItem;

            // Removing removable element
            ProjectListBox.Items.Remove(selected);
            // Insert it in new position
            ProjectListBox.Items.Insert(newIndex, selected);
            // Restore selection
            ProjectListBox.SelectedIndex = newIndex;

            config.Prjcfg.Clear();
            foreach ( var item in ProjectListBox.Items )
                config.Prjcfg.Add(item as Project);
        }

        private void filedialog_button_Click( object sender, RoutedEventArgs e )
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".sln";
            dlg.Filter = "Solution Files |*.sln";

            bool? result = dlg.ShowDialog();
            if ( result == true )
                Projectpath_textbox.Text = dlg.FileName;
        }



        private void RunDaily( )
        {
            if ( m_timepicker.Value == null )
                return;

            ScheduleTimer.Interval = GetTriggerTimeSpan();
            ScheduleTimer.Start();
        }

        private async void Scheduletimer_Tick( object sender, EventArgs e )
        {
            ScheduleTimer.Stop();
            ScheduleTimer.Interval = GetTriggerTimeSpan(); //set next tick timespan
            ScheduleTimer.Start();

            //fetch code option checked
            if ( FetchOnLaunch_checkbox.IsChecked == true )
            {
                Launch.IsEnabled = false;
                string userName = tfs_username_textbox.Text;
                string userPass = pw_passwordbox.Password;
                string tfsPath = tfs_path_textbox.Text;
                string tfsWorkSpace = tfs_workspace_textbox.Text;
                string requestPath = requestpath_textbox.Text;

                controller = await this.ShowProgressAsync("Please wait", "Downloading...", true);
                GetOptions opts = GetFetchOptions();
                await Task.Run(( ) => FetchCode(userName, userPass, tfsPath, tfsWorkSpace, requestPath, opts));
                Launch.IsEnabled = true;
                await controller.CloseAsync();
            }
            //launch builds
            LaunchButton_OnClick(sender, new RoutedEventArgs());
        }

        TimeSpan GetTriggerTimeSpan( )
        {
            var sched_time = (DateTime)m_timepicker.Value;
            var now = DateTime.Now;

            while ( now > sched_time )
            {
                sched_time = sched_time.AddDays(1.0);
                // Upgrate value of next launch time
                m_timepicker.Value = sched_time;
            }

            var timespan = sched_time - now;
            return timespan;
        }

        private void runschedule_btn_Click( object sender, RoutedEventArgs e )
        {
            if ( m_timepicker.Value == null )
            {
                System.Windows.MessageBox.Show("Pick scheduled time");
                return;
            }
            if ( ScheduleTimer.IsEnabled )
            {
                ScheduleTimer.Stop();
                runschedule_btn.Content = "schedule";
                Launch.IsEnabled = true;
            }
            else
            {
                RunDaily();
                runschedule_btn.Content = "cancel";
                Launch.IsEnabled = false;
            }
        }

        private void calculatestats_btn_Click( object sender, RoutedEventArgs e )
        {
            if ( ProjectListBox.SelectedIndex >= 0 )
            {
                Stats stats = new Stats();
                stats.Calculate(( ProjectListBox.Items[ProjectListBox.SelectedIndex] as Project ).buildTimes);
                TimeSpan mutime = new TimeSpan((long)stats.Mu);
                TimeSpan sigmatime = new TimeSpan((long)stats.Sigma);
                mu_tbx.Text = mutime.ToString(@"hh':'mm':'ss");
                sigma_tbx.Text = sigmatime.ToString(@"hh':'mm':'ss");
            }
        }

    }
}
