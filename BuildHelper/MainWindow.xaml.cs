using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using System.Xml.Serialization;
using System.IO;
using MahApps.Metro.Controls;
using Xceed.Wpf.Toolkit;

namespace BuildHelper
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : MetroWindow
	{
		private bool bBuildsLaunched = false;
		private List<Project> Config = new List<Project>();
		private int Index = 0;
		private string currentDir = Directory.GetCurrentDirectory();
		public MainWindow( )
		{
			InitializeComponent();
			LoadConfig();
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
			bBuildsLaunched = true;
			Launch.Content = "Cancel builds";
		}

		//
		//Build checkboxes checked event handlers
		//
		
		private void x64R_checkbox_CheckedChange( object sender, RoutedEventArgs e )
		{
			if ( ProjectListBox.SelectedIndex < 0 )
				return;
			Config[ProjectListBox.SelectedIndex].isChecked_x64R = (bool)( (CheckBox)sender ).IsChecked;
			SaveConfig();
		}

		private void x64D_checkbox_CheckedChange( object sender, RoutedEventArgs e )
		{
			if ( ProjectListBox.SelectedIndex < 0 )
				return;
			Config[ProjectListBox.SelectedIndex].isChecked_x64D = (bool)( (CheckBox)sender ).IsChecked;
			SaveConfig();
		}

		private void x86R_checkbox_CheckedChange( object sender, RoutedEventArgs e )
		{
			if ( ProjectListBox.SelectedIndex < 0 )
				return;
			Config[ProjectListBox.SelectedIndex].isChecked_x86R = (bool)( (CheckBox)sender ).IsChecked;
			SaveConfig();
		}

		private void x86D_checkbox_CheckedChange( object sender, RoutedEventArgs e )
		{
			if ( ProjectListBox.SelectedIndex < 0 )
				return;
			Config[ProjectListBox.SelectedIndex].isChecked_x86D = (bool)( (CheckBox)sender ).IsChecked;
			SaveConfig();
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
			SaveConfig();
		}

		private void removeproject_button_Click( object sender, RoutedEventArgs e )
		{
			if ( ProjectListBox.SelectedIndex < 0 )
				return;
			Config.RemoveAt(ProjectListBox.SelectedIndex);
			ProjectListBox.Items.Clear();
			foreach ( var item in Config )
				ProjectListBox.Items.Add(item);
			SaveConfig();
		}

		public bool LoadConfig(string cfgname = "config.xml")
		{
			if ( !File.Exists(currentDir + @"\" + cfgname) )
				return true;

			try
			{
				using ( FileStream fs = new FileStream(currentDir + @"\" + cfgname, FileMode.OpenOrCreate) )
				{
					XmlReader reader = XmlReader.Create(fs);
					XmlSerializer ser = new XmlSerializer(typeof(List<Project>));
					Config = (List<Project>)ser.Deserialize(reader);
					ProjectListBox.Items.Clear();
					foreach ( var item in Config )
						ProjectListBox.Items.Add(item);
				}
			}
			catch(Exception ex){ }
			return true;
		}

		public void SaveConfig(string cfgname = "config.xml")
		{
			using (FileStream fs = new FileStream(currentDir + @"\" + cfgname, FileMode.Create))
			{
				XmlWriter writer = XmlWriter.Create(fs);
				XmlSerializer ser = new XmlSerializer(typeof(List<Project>));
				ser.Serialize(writer, Config);
			}
		}

		private void Projectpath_textbox_TextChanged( object sender, TextChangedEventArgs e )
		{
			if ( !File.Exists(Projectpath_textbox.Text) )
				Projectpath_textbox.Background = Brushes.Red;
			else
				Projectpath_textbox.Background = Brushes.White;
		}

			
	}

	public enum VCS { TFS, GIT }


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
		public Project( ) { }
	}
}
