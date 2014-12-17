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
		private List<Project> Config = new List<Project>();
		private TFSAccount tfscfg = new TFSAccount();
		
		public MainWindow( )
		{
			InitializeComponent();
			CfgMan.LoadConfig(ref Config);
			CfgMan.LoadConfig(ref tfscfg);

			foreach ( var item in Config )
				ProjectListBox.Items.Add(item);
			tfs_username_textbox.Text = tfscfg.UserName;
			tfs_path_textbox.Text = tfscfg.TfsPath;
			tfs_workspace_textbox.Text = tfscfg.TfsWorkspace;
			requestpath_textbox.Text = tfscfg.RequestPath;
		}

		private void LaunchButton_OnClick(object sender, EventArgs e)
		{
			status_progressRing.IsActive = !status_progressRing.IsActive;
			if ( bBuildsLaunched )
			{
				//TODO: cancel builds
				Launch.Content = "Launch builds!";
				return;
			}
			//TODO: launch builds
			List<Process> ProcessPool = new List<Process>();
			for ( int i=0; i < Config.Count; i++ )
			{ 
				ProcessStartInfo start = new ProcessStartInfo();
				start.FileName = "C:/Program Files (x86)/Microsoft Visual Studio 12.0/Common7/IDE/devenv.com";
				start.UseShellExecute = false;
				start.RedirectStandardOutput = true;
				start.Arguments = Config[i].ProjectPath + @" /REBUILD debug|x64";
				ProcessPool.Add(Process.Start(start));
			}
			string result;
			while ( ( result = ProcessPool[0].StandardOutput.ReadLine() ) != null )
				Trace.WriteLine(result);

				bBuildsLaunched = true;
			Launch.Content = "Cancel builds";
		}

		

		private void x64R_checkbox_CheckedChange( object sender, RoutedEventArgs e )
		{
			if ( ProjectListBox.SelectedIndex < 0 )
				return;
			Config[ProjectListBox.SelectedIndex].isChecked_x64R = (bool)( (CheckBox)sender ).IsChecked;
			Config.SaveConfig();
		}

		private void x64D_checkbox_CheckedChange( object sender, RoutedEventArgs e )
		{
			if ( ProjectListBox.SelectedIndex < 0 )
				return;
			Config[ProjectListBox.SelectedIndex].isChecked_x64D = (bool)( (CheckBox)sender ).IsChecked;
			Config.SaveConfig();
		}

		private void x86R_checkbox_CheckedChange( object sender, RoutedEventArgs e )
		{
			if ( ProjectListBox.SelectedIndex < 0 )
				return;
			Config[ProjectListBox.SelectedIndex].isChecked_x86R = (bool)( (CheckBox)sender ).IsChecked;
			Config.SaveConfig();
		}

		private void x86D_checkbox_CheckedChange( object sender, RoutedEventArgs e )
		{
			if ( ProjectListBox.SelectedIndex < 0 )
				return;
			Config[ProjectListBox.SelectedIndex].isChecked_x86D = (bool)( (CheckBox)sender ).IsChecked;
			Config.SaveConfig();
		}

		private void ProjectListBox_SelectionChanged( object sender, SelectionChangedEventArgs e )
		{
			if ( ProjectListBox.SelectedIndex < 0 )
				return;
			x64D_checkbox1.IsChecked = Config[ProjectListBox.SelectedIndex].isChecked_x64D;
			x64R_checkbox1.IsChecked = Config[ProjectListBox.SelectedIndex].isChecked_x64R;
			x86R_checkbox1.IsChecked = Config[ProjectListBox.SelectedIndex].isChecked_x86R;
			x86D_checkbox1.IsChecked = Config[ProjectListBox.SelectedIndex].isChecked_x86D;
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
			Config.Add(proj);
			Config.SaveConfig();
		}

		private void removeproject_button_Click( object sender, RoutedEventArgs e )
		{
			if ( ProjectListBox.SelectedIndex < 0 )
				return;
			Config.RemoveAt(ProjectListBox.SelectedIndex);
			ProjectListBox.Items.Clear();
			foreach ( var item in Config )
				ProjectListBox.Items.Add(item);
			Config.SaveConfig();
		}

		private void Projectpath_textbox_TextChanged( object sender, TextChangedEventArgs e )
		{
			if ( !File.Exists(Projectpath_textbox.Text) )
				Projectpath_textbox.Background = Brushes.Red;
			else
				Projectpath_textbox.Background = Brushes.White;
		}

		private void FetchButton_OnClick( object sender, RoutedEventArgs e )
		{
			FetchCode();
		}

		private void FetchCode()
		{
			try
			{
				string userName = tfs_username_textbox.Text;
				string userPass = pw_passwordbox.Password;
				string tfsPath = tfs_path_textbox.Text;
				string tfsWorkSpace = tfs_workspace_textbox.Text;
				string requestPath = requestpath_textbox.Text;
				ICredentials myCred = new NetworkCredential(userName, userPass);
				TeamFoundationServer tfs = new TeamFoundationServer(tfsPath, myCred);
				VersionControlServer vcs = tfs.GetService<VersionControlServer>();
				Workspace myWorkspace = vcs.GetWorkspace(tfsWorkSpace, vcs.AuthorizedUser);
				GetRequest request = new GetRequest(new ItemSpec(requestPath, RecursionType.Full), VersionSpec.Latest);
				GetStatus getStat = myWorkspace.Get(request, GetOptions.None);
			}
			catch(Exception ex)
			{
				MessageBox.Show("Error during fetching code: " + ex.Message);
			}
		}

		private void FetchCheckBox_Click( object sender, RoutedEventArgs e )
		{
			if ( fetchcode_button != null)
				fetchcode_button.IsEnabled = (bool)FetchOnLaunch_checkbox.IsChecked;
		}

		private void rememberTFScfg_click( object sender, RoutedEventArgs e )
		{
			if ( tfscfg == null )
				tfscfg = new TFSAccount();
			tfscfg.UserName = tfs_username_textbox.Text;
			tfscfg.TfsPath = tfs_path_textbox.Text;
			tfscfg.TfsWorkspace = tfs_workspace_textbox.Text;
			tfscfg.RequestPath = requestpath_textbox.Text;
			CfgMan.SaveConfig(tfscfg);
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
		public VCS projectVCS = VCS.TFS;
		public bool isChecked_x86D = false;
		public bool isChecked_x86R = false;
		public bool isChecked_x64D = false;
		public bool isChecked_x64R = false;
		public override string ToString( )
		{
			return ProjectName;
		}
	}

	public static class CfgMan
	{
		private static string currentDir = Directory.GetCurrentDirectory();
		public static void SaveConfig(this object obj)
		{
			string cfgname = String.Empty;
			Type type = obj.GetType();

			if ( type == typeof(List<Project>) )
				cfgname = "config.xml";
			else
				cfgname = "tfsconfig.xml";
			using ( FileStream fs = new FileStream(currentDir + @"\" + cfgname, FileMode.Create) )
			{
				XmlWriter writer = XmlWriter.Create(fs);
				XmlSerializer ser = new XmlSerializer(type);
				ser.Serialize(writer, obj);
			}
		}
		public static void LoadConfig(ref List<Project> cfg )
		{
			string cfgname = "config.xml";
			if ( !File.Exists(currentDir + @"\" + cfgname) )
				return;
			try
			{
				using ( FileStream fs = new FileStream(currentDir + @"\" + cfgname, FileMode.OpenOrCreate) )
				{
					XmlReader reader = XmlReader.Create(fs);
					XmlSerializer ser = new XmlSerializer(typeof(List<Project>));
					cfg = (List<Project>)ser.Deserialize(reader);
				}
			}
			catch ( Exception ex ) { }
		}
		public static void LoadConfig(ref TFSAccount cfg)
		{
			string cfgname = "tfsconfig.xml";
			if ( !File.Exists(currentDir + @"\" + cfgname) )
				return;
			try
			{
				using ( FileStream fs = new FileStream(currentDir + @"\" + cfgname, FileMode.OpenOrCreate) )
				{
					XmlReader reader = XmlReader.Create(fs);
					XmlSerializer ser = new XmlSerializer(typeof(TFSAccount));
					cfg = (TFSAccount)ser.Deserialize(reader);
				}
			}
			catch ( Exception ex ) { }

		}
	}
}
