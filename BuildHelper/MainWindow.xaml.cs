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
using MahApps.Metro.Controls;

namespace BuildHelper
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : MetroWindow
	{
		bool bBuildsLaunched = false;
		public MainWindow( )
		{
			InitializeComponent();
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
	}
}
