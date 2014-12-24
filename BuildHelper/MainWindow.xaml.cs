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
using System.Xml;
using System.Threading;
using System.Xml.Serialization;
using System.IO;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.Net;
using System.ComponentModel;
using Microsoft.TeamFoundation;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.VersionControl.Client;

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
		private int num_procexited = 0;
		private object locker = new object();
		DispatcherTimer timer = new DispatcherTimer();
		DateTime startTime;
		DateTime projstarttime;
		public MainWindow( )
		{
			InitializeComponent();
			
			config.LoadConfig();

			foreach ( var item in config.Prjcfg )
				ProjectListBox.Items.Add(item);
			tfs_username_textbox.Text = config.Tfscfg.UserName;
			tfs_path_textbox.Text = config.Tfscfg.TfsPath;
			tfs_workspace_textbox.Text = config.Tfscfg.TfsWorkspace;
			requestpath_textbox.Text = config.Tfscfg.RequestPath;
			timer.Interval = new TimeSpan(0, 0, 1);
			timer.Tick += dispatcherTimer_Tick;
		}

		private void dispatcherTimer_Tick( object sender, EventArgs e )
		{
			// Updating the Label which displays the current second
			timer_label.Content = (DateTime.Now - startTime).ToString();
		}

		private void LaunchButton_OnClick(object sender, EventArgs e)
		{
			if(config.Prjcfg.Count == 0)
			{
				MessageBox.Show("Config is empty");
				return;
			}
			status_progressRing.IsActive = !status_progressRing.IsActive;
			if (bBuildsLaunched)
			{
				StopBuild();
				return;
			}
			startTime = DateTime.Now;
			timer.Start();
			CreateBuildQueue();
			StartBuild();
			output_listbox.Items.Add("BUILDS LAUNCHED!");
			bBuildsLaunched = true;
			Launch.Content = "Cancel builds";
			
		}

		private void StartBuild()
		{
			try
			{
				Task.Run(() => BuildQueue.Peek().Start());
				projstarttime = DateTime.Now;
				Task.Run(() => WriteOutput(BuildQueue.Peek()));
			}
			catch ( Exception ex )
			{
				MessageBox.Show("Failed to launch build:" + ex.Message);
			}
		}

		private void StopBuild()
		{
			if ( BuildQueue.Count > 0)
				BuildQueue.Peek().Close();
			BuildQueue.Clear();
			timer.Stop();
			timer_label.Content = "";
			output_listbox.Items.Add("BUILDS CANCELLED!");
			Launch.Content = "Launch builds!";
			bBuildsLaunched = false;
		}

		private void WriteOutput(Process proc)
		{
			Thread.Sleep(100);
			string result = proc.StandardOutput.ReadLine();
			while ( result != null )
			{
				output_listbox.Dispatcher.Invoke((Action)( ( ) =>
					{
						output_listbox.Items.Add(result);
						output_listbox.ScrollIntoView(output_listbox.Items[output_listbox.Items.Count-1]);
					}));
				result = proc.StandardOutput.ReadLine();
			}
		}

		private void CreateBuildQueue()
		{
			foreach ( var elem in ProjectListBox.Items )
			{
				Project proj = elem as Project;
				List<string> rebuildInfo = proj.GetRebuildInfoList();
				foreach(var arg in rebuildInfo)
				{
					Process process = new Process();
					process.StartInfo.FileName = "C:/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/devenv.com";
					process.StartInfo.UseShellExecute = false;
					process.StartInfo.RedirectStandardOutput = true;
					process.StartInfo.Arguments = proj.ProjectPath + @" /REBUILD " + arg;
					process.EnableRaisingEvents = true;
					process.Exited += ProcExited;
					BuildQueue.Enqueue(process);
				}
			}
		}

		private void ProcExited(object sender, EventArgs e)
		{
			this.Dispatcher.Invoke(( ) =>
				{
					//status_progressRing.IsActive = !status_progressRing.IsActive;
					string projName = ( sender as Process ).StartInfo.Arguments;
					output_listbox.Items.Add(projName + ": " + (projstarttime - DateTime.Now).ToString());
					BuildQueue.Dequeue().Close();
					if ( BuildQueue.Count == 0 )
					{
						timer.Stop();
						Launch.Content = "Launch builds!";
						status_progressRing.IsActive = !status_progressRing.IsActive;
						bBuildsLaunched = false;
					}
					else
						StartBuild();
				});
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

		private void createProject_button_Click( object sender, RoutedEventArgs e )
		{
			if(String.IsNullOrEmpty(Projectname_textbox.Text))
			{
				System.Windows.MessageBox.Show("Project name is empty");
				return;
			}
			if ( !File.Exists(Projectpath_textbox.Text))
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

		private void Projectpath_textbox_TextChanged( object sender, TextChangedEventArgs e )
		{
			if ( !File.Exists(Projectpath_textbox.Text) )
				Projectpath_textbox.Background = Brushes.Red;
			else
				Projectpath_textbox.Background = Brushes.White;
		}

		

		private async void FetchButton_OnClick( object sender, RoutedEventArgs e )
		{
			Launch.IsEnabled = false;
			string userName = tfs_username_textbox.Text;
			string userPass = pw_passwordbox.Password;
			string tfsPath = tfs_path_textbox.Text;
			string tfsWorkSpace = tfs_workspace_textbox.Text;
			string requestPath = requestpath_textbox.Text;
			
			var controller = await this.ShowProgressAsync("Please wait", "Downloading...");
			await Task.Run( ()=> FetchCode(userName, userPass, tfsPath, tfsWorkSpace, requestPath));
			Launch.IsEnabled = true;
			await controller.CloseAsync();

		}

		private void FetchCode(string userName, string userPass, string tfsPath, string tfsWorkSpace, string requestPath)
		{
			Thread.Sleep(5000);
			GetStatus getStat = null;
			try
			{
				ICredentials myCred = new NetworkCredential(userName, userPass);
				TeamFoundationServer tfs = new TeamFoundationServer(tfsPath, myCred);
				VersionControlServer vcs = tfs.GetService<VersionControlServer>();
				Workspace myWorkspace = vcs.GetWorkspace(tfsWorkSpace, vcs.AuthorizedUser);
				GetRequest request = new GetRequest(new ItemSpec(requestPath, RecursionType.Full), VersionSpec.Latest);
				getStat = myWorkspace.Get(request, GetOptions.None);
			}
			catch ( Exception ex )
			{
				MessageBox.Show("Fetching code failed: " + ex.Message);
			}
			if (getStat == null || getStat.NumFailures > 0 || getStat.NumWarnings > 0)
			{
				output_listbox.Dispatcher.Invoke((Action)( () =>
				{ 
					output_listbox.Items.Add("Errors while getting latest have occurred");
				}));
				return;
			}

			if ( getStat.NumOperations == 0 )
			{
				MessageBox.Show("All files are up to date");
			}
			else
				output_listbox.Dispatcher.Invoke((Action)( ( ) =>
				{
					output_listbox.Items.Add("Successfully downloaded code");
				} ));
		}

		private void FetchCheckBox_Click( object sender, RoutedEventArgs e )
		{
			if ( fetchcode_button != null)
				fetchcode_button.IsEnabled = (bool)FetchOnLaunch_checkbox.IsChecked;
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

		private void OnListView_ItemsAdded(object sender, EventArgs e)
		{
			
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
	}

	public enum VCS { TFS, GIT }

	[XmlInclude(typeof(TFSAccount))]
	public class TFSAccount
	{
		public string UserName { get; set; }
		public string TfsPath { get; set; }
		public string TfsWorkspace { get; set; }
		public string RequestPath { get; set; }
		public string PassWord { get; set; }
		public TFSAccount()
		{
			UserName = String.Empty;
			TfsPath = String.Empty;
			TfsWorkspace = String.Empty;
			RequestPath = String.Empty;
			PassWord = String.Empty;
		}
	}

	[XmlInclude(typeof(Project))]
	public class Project
	{
		public string ProjectName = String.Empty;
		public string ProjectPath = String.Empty;
		public VCS projectVCS = VCS.TFS; //TODO
		public bool x86D = false;
		public bool x86R = false;
		public bool x64D = false;
		public bool x64R = false;
		public override string ToString( )
		{
			return ProjectName;
		}
		public List<string> GetRebuildInfoList( )
		{
			List<string> ret = new List<string>();
			if ( x64D )
				ret.Add(@"Debug|x64");
			if ( x86D )
				ret.Add(@"Debug|x86");
			if ( x64R )
				ret.Add(@"Release|x64");
			if ( x86R )
				ret.Add(@"Release|x86");
			return ret;
		}
		public byte GetBitFieldConfig()
		{
			byte field = 0;
			if ( x86D )
				field |= 1;
			if ( x86R )
				field |= 1 << 1;
			if ( x64D )
				field |= 1 << 2;
			if ( x64R )
				field |= 1 <<3;
			return field;
		}
	}

	public class CfgMan
	{
		public List<Project> Prjcfg = new List<Project>();
		public TFSAccount Tfscfg = new TFSAccount();
		private static string currentDir = Directory.GetCurrentDirectory();

		public void SaveConfig()
		{
			Serialize(Prjcfg, "config.xml");
			Serialize(Tfscfg, "tfsconfig.xml");
		}

		public void LoadConfig()
		{
			Deserialize(ref Prjcfg, "config.xml");
			Deserialize(ref Tfscfg, "tfsconfig.xml");
		}

		private void Serialize<T>(T cfg, string path)
		{
			try
			{
				using ( FileStream fs = new FileStream(currentDir + @"\" + path, FileMode.Create) )
				{
					XmlWriter writer = XmlWriter.Create(fs);
					XmlSerializer ser = new XmlSerializer(typeof(T));
					ser.Serialize(writer, cfg);
				}
			}
			catch(Exception ex)
			{
				MessageBox.Show("Error during saving config: " + ex.Message);
			}
		}
		private void Deserialize<T>(ref T cfg, string path)
		{
			if ( !File.Exists(currentDir + @"\" + path) )
				return;
			try
			{
				using ( FileStream fs = new FileStream(currentDir + @"\" + path, FileMode.OpenOrCreate) )
				{
					XmlReader reader = XmlReader.Create(fs);
					XmlSerializer ser = new XmlSerializer(typeof(T));
					cfg = (T)ser.Deserialize(reader);
				}
			}
			catch ( Exception ex )
			{
				MessageBox.Show("Error during loading config: " + ex.Message);
			}
		}
	}
}
