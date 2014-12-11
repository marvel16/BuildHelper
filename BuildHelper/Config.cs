using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace BuildHelper
{
	public enum VCS {TFS, GIT}

	[Serializable]
	public class Project
	{
		public string ProjectName = String.Empty;
		public string ProjectPath = String.Empty;
		public VCS projectVCS = VCS.TFS;
		public bool isChecked_x86D = false;
		public bool isChecked_x86R = false;
		public bool isChecked_x64D = false;
		public bool isChecked_x64R = false;

		public Project( ) { }
	}

	[Serializable]
	public class ConfigFile
	{
		public List<Project> Projects { get; set; }
		public ConfigFile( ) { }
	}

	public static class ConfigManager
	{
		public static ConfigFile CFG {set; get;}
		public static void LoadConfig(string path)
		{
			if (!File.Exists(path))
				return;
			
			using (XmlReader reader = XmlReader.Create(path))
			{
				XmlSerializer serializer = new XmlSerializer(typeof(ConfigFile));
				CFG = (ConfigFile)serializer.Deserialize(reader);
			}
		}

		public static bool SaveConfig(string path)
		{
			if (String.IsNullOrEmpty(path) | CFG == null)
				return false;
			
			using ( StreamWriter writer = new StreamWriter(path, false) )
			{
				XmlSerializer serializer = new XmlSerializer(typeof(ConfigFile));
				serializer.Serialize(writer, CFG);
			}
			return true;
		}

	}
}
