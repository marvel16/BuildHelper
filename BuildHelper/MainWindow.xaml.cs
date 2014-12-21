using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
		private List<Process> ProcessPool = new List<Process>();
		
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
				foreach (Process proc in ProcessPool)
					proc.Close();
				ProcessPool.Clear();
				output_listbox.Items.Add("BUILDS CANCELLED!");
				Launch.Content = "Launch builds!";
				bBuildsLaunched = !bBuildsLaunched;
				return;
			}

			StartBuilds(); //launch devenv process
			output_listbox.Items.Add("BUILDS LAUNCHED!");
			bBuildsLaunched = true;
			Launch.Content = "Cancel builds";
			Task.Run(() => WriteOutput());
			
			
		}

		private void WriteOutput()
		{
			string result = ProcessPool[0].StandardOutput.ReadLine();
			while ( result != null )
			{
				output_listbox.Dispatcher.Invoke((Action)( ( ) => output_listbox.Items.Add(result) ));
				result = ProcessPool[0].StandardOutput.ReadLine();
			}
		}

		private void StartBuilds()
		{
			for ( int i=0; i < config.Prjcfg.Count; i++ )
			{
				string rebuildInfo = ParseConfig(config.Prjcfg[i]);
				ProcessStartInfo start = new ProcessStartInfo();
				start.FileName = "C:/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/devenv.com";
				start.UseShellExecute = false;
				start.RedirectStandardOutput = true;
				start.Arguments = config.Prjcfg[i].ProjectPath + @" /REBUILD " + rebuildInfo;
				try
				{
					ProcessPool.Add(Process.Start(start));
				}
				catch(Exception ex)
				{
					MessageBox.Show("Failed to launch builds:" + ex.Message);
				}
			}
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
			x64D_checkbox1.IsChecked = config.Prjcfg[ProjectListBox.SelectedIndex].x64D;
			x64R_checkbox1.IsChecked = config.Prjcfg[ProjectListBox.SelectedIndex].x64R;
			x86R_checkbox1.IsChecked = config.Prjcfg[ProjectListBox.SelectedIndex].x86R;
			x86D_checkbox1.IsChecked = config.Prjcfg[ProjectListBox.SelectedIndex].x86D;
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

		private string ParseConfig(Project sol)
		{
			string ret = string.Empty;
			if ( sol.x64D )
				ret = @"Debug|x64";
			if ( sol.x86D )
				ret = @"Debug|x86";
			if ( sol.x64R )
				ret = @"Release|x64";
			if ( sol.x86R )
				ret = @"Release|x86";
			return ret;
		}

		private void FetchButton_OnClick( object sender, RoutedEventArgs e )
		{
			FetchCode();
		}

		private void FetchCode( )
		{
			string userName = tfs_username_textbox.Text;
			string userPass = pw_passwordbox.Password;
			string tfsPath = tfs_path_textbox.Text;
			string tfsWorkSpace = tfs_workspace_textbox.Text;
			string requestPath = requestpath_textbox.Text;

			try
			{
				ICredentials myCred = new NetworkCredential(userName, userPass);
				TeamFoundationServer tfs = new TeamFoundationServer(tfsPath, myCred);
				VersionControlServer vcs = tfs.GetService<VersionControlServer>();
				Workspace myWorkspace = vcs.GetWorkspace(tfsWorkSpace, vcs.AuthorizedUser);
				GetRequest request = new GetRequest(new ItemSpec(requestPath, RecursionType.Full), VersionSpec.Latest);
				GetStatus getStat = myWorkspace.Get(request, GetOptions.None);
			}
			catch(Exception ex)
			{
				MessageBox.Show("TFS Connection failed: " + ex.Message);
			}
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
			config.SaveConfig();
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
		public TFSAccount()
		{
			UserName = String.Empty;
			TfsPath = String.Empty;
			TfsWorkspace = String.Empty;
			RequestPath = String.Empty;
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
