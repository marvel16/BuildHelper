using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.Net;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;
using System.Management;
using System.Text.RegularExpressions;

namespace BuildHelper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private bool _bBuildsLaunched = false;
        private CfgMan _config = new CfgMan();
        private List<CheckBox> _checkboxes = new List<CheckBox>();
        private Queue<Process> _buildQueue = new Queue<Process>();
        private object _locker = new object();
        DispatcherTimer _timer = new DispatcherTimer();
        DispatcherTimer _scheduleTimer = new DispatcherTimer();

        DateTime _startTime;
        DateTime _projstarttime;
        ProgressDialogController _controller;
        private static long _itemCount = 0;

        public MainWindow( )
        {
            InitializeComponent();
            _config.LoadConfig();

            foreach ( var item in _config.Prjcfg )
                ProjectListBox.Items.Add(item);
            //initialize field from config
            TfsUsernameTextbox.Text = _config.Tfscfg.UserName;
            TfsPathTextbox.Text = _config.Tfscfg.TfsPath;
            TfsWorkspaceTextbox.Text = _config.Tfscfg.TfsWorkspace;
            PwPasswordbox.Password = _config.Tfscfg.PassWord;
            RequestpathTextbox.Text = _config.Tfscfg.RequestPath;

            //Tag radioButtons with resolve confict option
            NoautroresolveRbx.Tag = GetOptions.NoAutoResolve;
            NoneRbx.Tag = GetOptions.None;
            OverwriteRbx.Tag = GetOptions.Overwrite;

            MTimepicker.Value = DateTime.Now;
            _timer.Interval = new TimeSpan(0, 0, 1);
            _timer.Tick += dispatcherTimer_Tick;
            _scheduleTimer.Tick += Scheduletimer_Tick;

        }

        private void dispatcherTimer_Tick( object sender, EventArgs e )
        {
            // Updating the Label which displays the current second
            TimerLabel.Content = ( DateTime.Now - _startTime ).ToString(@"hh':'mm':'ss");
        }

        private void LaunchButton_OnClick( object sender, EventArgs e )
        {
            if ( _config.Prjcfg.Count == 0 )
            {
                System.Windows.MessageBox.Show("Config is empty");
                return;
            }
            StatusProgressRing.IsActive = !StatusProgressRing.IsActive;

            if ( _bBuildsLaunched )
            {
                StopBuild();
                return;
            }
            _startTime = DateTime.Now;
            if ( _scheduleTimer.IsEnabled == false )
                OutputListbox.Items.Clear();
            Launch.Content = "Cancel builds";
            _timer.Start();
            CreateBuildQueue();
            StartBuild();
        }

        private void CreateBuildQueue( )
        {
            foreach ( var elem in ProjectListBox.Items )
            {
                Project proj = elem as Project;
                if (proj != null)
                {
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
                        _buildQueue.Enqueue(process);
                    }
                }
            }
        }

        private void StartBuild( )
        {
            _bBuildsLaunched = true;

            OutputListbox.Items.Add("BUILDS LAUNCHED!");

            try
            {
                _buildQueue.Peek().Start();
                _projstarttime = DateTime.Now;
                Task.Run(( ) => ReadOutput(_buildQueue.Peek()));
            }
            catch ( Exception ex )
            {
                System.Windows.MessageBox.Show("Failed to launch build:" + ex.Message);
            }
        }

        private void StopBuild( )
        {
            _bBuildsLaunched = false;
            if ( _buildQueue.Count > 0 )
                KillProcessAndChildren(_buildQueue.Peek().Id);
            _buildQueue.Clear();
            _timer.Stop();
            _scheduleTimer.Stop();
            TimerLabel.Content = "";
            OutputListbox.Items.Add("BUILDS CANCELLED!");
            Launch.Content = "Launch builds!";

        }

        private static void KillProcessAndChildren( int pid )
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher
              ("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (var mo in moc.Cast<ManagementObject>())
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
                OutputListbox.Dispatcher.Invoke(
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
                            OutputListbox.Items.Add(li);
                            OutputListbox.ScrollIntoView(li);
                        }
                    });
            }
        }

        private void ProcExited( object sender, EventArgs e )
        {
            Process process = sender as Process;
            if (process != null)
            {
                string projName = process.StartInfo.Arguments;
                TimeSpan buildtime = DateTime.Now - _projstarttime;
                Task.Run(( ) => AddBuildTime(projName, buildtime.Ticks));
                if ( _bBuildsLaunched )
                    this.Dispatcher.Invoke(( ) =>
                    {
                        OutputListbox.Items.Add(projName + ": " + buildtime.ToString(@"hh':'mm':'ss"));
                        //make sure no children processes alive
                        KillProcessAndChildren(_buildQueue.Peek().Id);
                        _buildQueue.Dequeue();
                        if ( _buildQueue.Count == 0 )
                        {
                            _timer.Stop();
                            Launch.Content = "Launch builds!";
                            StatusProgressRing.IsActive = !StatusProgressRing.IsActive;
                            _bBuildsLaunched = false;
                            Launch.IsEnabled = true;
                        }
                        else
                            StartBuild();
                    });
            }
        }

        private void AddBuildTime( string arg, long time )
        {
            foreach (var proj in _config.Prjcfg.Where(proj => arg.Contains(proj.ProjectPath)))
            {
                proj.BuildTimes.Add(time);
                _config.SaveConfig();
                return;
            }
        }

        private GetOptions GetFetchOptions( )
        {
            var rbx = SrcGrid.Children.OfType<RadioButton>().FirstOrDefault(r => (bool)r.IsChecked);
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
                _itemCount = 0;// reset server items counter
                GetRequest request = new GetRequest(new ItemSpec(requestPath, RecursionType.Full), VersionSpec.Latest);

                getStat = myWorkspace.Get(request, opts);
            }
            catch ( Exception ex )
            {
                OutputListbox.Dispatcher.Invoke(( ) =>
                {
                    OutputListbox.Items.Add("Fetching code failed with exception: " + ex.Message);
                });
                return;
            }
            if ( getStat == null || getStat.NumFailures > 0 || getStat.NumWarnings > 0 )
            {
                OutputListbox.Dispatcher.Invoke(( ) =>
                {
                    OutputListbox.Items.Add("Errors while getting latest have occurred");
                });
                return;
            }

            if ( getStat.NumOperations == 0 )
            {
                OutputListbox.Dispatcher.Invoke(( ) =>
                {
                    OutputListbox.Items.Add("All files are up to date");
                });
            }
            else
                OutputListbox.Dispatcher.Invoke((Action)( ( ) =>
                {
                    OutputListbox.Items.Add("Successfully downloaded code");
                } ));
        }

        private void x64R_checkbox_CheckedChange( object sender, RoutedEventArgs e )
        {
            if ( ProjectListBox.SelectedIndex < 0 )
                return;
            _config.Prjcfg[ProjectListBox.SelectedIndex].X64R = (bool)((CheckBox)sender).IsChecked;
            _config.SaveConfig();
        }

        private void x64D_checkbox_CheckedChange( object sender, RoutedEventArgs e )
        {
            if ( ProjectListBox.SelectedIndex < 0 )
                return;
            _config.Prjcfg[ProjectListBox.SelectedIndex].X64D = (bool)( (CheckBox)sender ).IsChecked;
            _config.SaveConfig();
        }

        private void x86R_checkbox_CheckedChange( object sender, RoutedEventArgs e )
        {
            if ( ProjectListBox.SelectedIndex < 0 )
                return;
            _config.Prjcfg[ProjectListBox.SelectedIndex].X86R = (bool)( (CheckBox)sender ).IsChecked;
            _config.SaveConfig();
        }

        private void x86D_checkbox_CheckedChange( object sender, RoutedEventArgs e )
        {
            if ( ProjectListBox.SelectedIndex < 0 )
                return;
            _config.Prjcfg[ProjectListBox.SelectedIndex].X86D = (bool)( (CheckBox)sender ).IsChecked;
            _config.SaveConfig();
        }

        private void ProjectListBox_SelectionChanged( object sender, SelectionChangedEventArgs e )
        {
            if ( ProjectListBox.SelectedIndex < 0 )
                return;
            X64DCheckbox1.IsChecked = ( ProjectListBox.Items[ProjectListBox.SelectedIndex] as Project ).X64D;
            X64RCheckbox1.IsChecked = ( ProjectListBox.Items[ProjectListBox.SelectedIndex] as Project ).X64R;
            X86RCheckbox1.IsChecked = ( ProjectListBox.Items[ProjectListBox.SelectedIndex] as Project ).X86R;
            X86DCheckbox1.IsChecked = ( ProjectListBox.Items[ProjectListBox.SelectedIndex] as Project ).X86D;
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            OutputListbox.Items.Clear();
        }

        private void OnClearLogFileClick(object sender, RoutedEventArgs e)
        {
            Logger.ClearLog();
        }

        private void createProject_button_Click( object sender, RoutedEventArgs e )
        {
            if ( String.IsNullOrEmpty(ProjectnameTextbox.Text) )
            {
                System.Windows.MessageBox.Show("Project name is empty");
                return;
            }
            if ( !File.Exists(ProjectpathTextbox.Text) )
            {
                System.Windows.MessageBox.Show("Project path not found");
                return;
            }

            var proj = new Project();
            proj.ProjectName = ProjectnameTextbox.Text;
            proj.ProjectPath = ProjectpathTextbox.Text;
            ProjectListBox.Items.Add(proj);
            _config.Prjcfg.Add(proj);
            _config.SaveConfig();
            ProjectnameTextbox.Clear();
            ProjectpathTextbox.Clear();
        }

        private void removeproject_button_Click( object sender, RoutedEventArgs e )
        {
            if ( ProjectListBox.SelectedIndex < 0 )
                return;
            _config.Prjcfg.RemoveAt(ProjectListBox.SelectedIndex);
            ProjectListBox.Items.Clear();
            foreach ( var item in _config.Prjcfg )
                ProjectListBox.Items.Add(item);
            _config.SaveConfig();
        }

        private async void FetchButton_OnClick( object sender, RoutedEventArgs e )
        {
            Launch.IsEnabled = false;
            string userName = TfsUsernameTextbox.Text;
            string userPass = PwPasswordbox.Password;
            string tfsPath = TfsPathTextbox.Text;
            string tfsWorkSpace = TfsWorkspaceTextbox.Text;
            string requestPath = RequestpathTextbox.Text;

            _controller = await this.ShowProgressAsync("Please wait", "Downloading...", true);
            GetOptions opts = GetFetchOptions();
            await Task.Run(( ) => FetchCode(userName, userPass, tfsPath, tfsWorkSpace, requestPath, opts));
            Launch.IsEnabled = true;
            await _controller.CloseAsync();

        }

        private void OnGettingEvent( object sender, GettingEventArgs e )
        {
            if ( e.Total == 0 )
                return;
            _itemCount++;
            double progress = _itemCount / (double)e.Total;
            this.Dispatcher.Invoke(( ) =>
            {
                if ( progress > 1 )
                    _controller.SetProgress((int)progress);
                else
                    _controller.SetProgress(progress);
                _controller.SetMessage("Downloading...\n" + _itemCount.ToString() + " of "+ e.Total.ToString() + ": "+ e.ServerItem);
            });
        }

        private void FetchCheckBox_Click( object sender, RoutedEventArgs e )
        {

        }

        private void rememberTFScfg_click( object sender, RoutedEventArgs e )
        {
            _config.Tfscfg.UserName = TfsUsernameTextbox.Text;
            _config.Tfscfg.TfsPath = TfsPathTextbox.Text;
            _config.Tfscfg.TfsWorkspace = TfsWorkspaceTextbox.Text;
            _config.Tfscfg.RequestPath = RequestpathTextbox.Text;
            _config.Tfscfg.PassWord = PwPasswordbox.Password;
            _config.SaveConfig();
        }


        private void On_moveup( object sender, RoutedEventArgs e )
        {
            MoveItem(-1);
        }

        private void On_movedown( object sender, RoutedEventArgs e )
        {
            MoveItem(1);
        }

        private void MoveItem( int direction )
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

            _config.Prjcfg.Clear();
            foreach ( var item in ProjectListBox.Items )
                _config.Prjcfg.Add(item as Project);
        }

        private void filedialog_button_Click( object sender, RoutedEventArgs e )
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".sln";
            dlg.Filter = "Solution Files |*.sln";

            bool? result = dlg.ShowDialog();
            if ( result == true )
                ProjectpathTextbox.Text = dlg.FileName;
        }



        private void RunDaily( )
        {
            if ( MTimepicker.Value == null )
                return;

            _scheduleTimer.Interval = GetTriggerTimeSpan();
            _scheduleTimer.Start();
        }

        private async void Scheduletimer_Tick( object sender, EventArgs e )
        {
            _scheduleTimer.Stop();
            _scheduleTimer.Interval = GetTriggerTimeSpan(); //set next tick timespan
            _scheduleTimer.Start();

            //fetch code option checked
            if ( FetchOnLaunchCheckbox.IsChecked == true )
            {
                Launch.IsEnabled = false;
                string userName = TfsUsernameTextbox.Text;
                string userPass = PwPasswordbox.Password;
                string tfsPath = TfsPathTextbox.Text;
                string tfsWorkSpace = TfsWorkspaceTextbox.Text;
                string requestPath = RequestpathTextbox.Text;

                _controller = await this.ShowProgressAsync("Please wait", "Downloading...", true);
                GetOptions opts = GetFetchOptions();
                await Task.Run(( ) => FetchCode(userName, userPass, tfsPath, tfsWorkSpace, requestPath, opts));
                Launch.IsEnabled = true;
                await _controller.CloseAsync();
            }
            //launch builds
            LaunchButton_OnClick(sender, new RoutedEventArgs());
        }

        TimeSpan GetTriggerTimeSpan( )
        {
            var schedTime = (DateTime)MTimepicker.Value;
            var now = DateTime.Now;

            while ( now > schedTime )
            {
                schedTime = schedTime.AddDays(1.0);
                // Upgrate value of next launch time
                MTimepicker.Value = schedTime;
            }

            var timespan = schedTime - now;
            return timespan;
        }

        private void runschedule_btn_Click( object sender, RoutedEventArgs e )
        {
            if ( MTimepicker.Value == null )
            {
                System.Windows.MessageBox.Show("Pick scheduled time");
                return;
            }
            if ( _scheduleTimer.IsEnabled )
            {
                _scheduleTimer.Stop();
                RunscheduleBtn.Content = "schedule";
                Launch.IsEnabled = true;
            }
            else
            {
                RunDaily();
                RunscheduleBtn.Content = "cancel";
                Launch.IsEnabled = false;
            }
        }

        private void calculatestats_btn_Click( object sender, RoutedEventArgs e )
        {
            if ( ProjectListBox.SelectedIndex >= 0 )
            {
                Stats stats = new Stats();
                stats.Calculate(( ProjectListBox.Items[ProjectListBox.SelectedIndex] as Project ).BuildTimes);
                TimeSpan mutime = new TimeSpan((long)stats.Mu);
                TimeSpan sigmatime = new TimeSpan((long)stats.Sigma);
                MuTbx.Text = mutime.ToString(@"hh':'mm':'ss");
                SigmaTbx.Text = sigmatime.ToString(@"hh':'mm':'ss");
            }
        }

    }
}
