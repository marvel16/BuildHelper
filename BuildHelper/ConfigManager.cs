using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using System.IO;

namespace BuildHelper
{
    public enum Vcs { Tfs, Git }

    [XmlInclude(typeof(TfsAccount))]
    public class TfsAccount
    {
        public string UserName { get; set; }
        public string TfsPath { get; set; }
        public string TfsWorkspace { get; set; }
        public string RequestPath { get; set; }
        public string PassWord { get; set; }
        public TfsAccount( )
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
        //public VCS projectVCS = VCS.TFS; //TODO
        public bool X86D = false;
        public bool X86R = false;
        public bool X64D = false;
        public bool X64R = false;

        [XmlElement(ElementName = "BuildTimes")]
        public List<long> BuildTimes = new List<long>();

        public override string ToString( )
        {
            return ProjectName;
        }
        public List<string> GetRebuildInfoList( )
        {
            List<string> ret = new List<string>();
            if ( X64D )
                ret.Add(@"Debug|x64");
            if ( X64R )
                ret.Add(@"Release|x64");
            if ( X86D )
                ret.Add("Debug|" + ResolveProjectType()); // C++ is win32, C# is x86
            if ( X86R )
                ret.Add("Release|" + ResolveProjectType());
            return ret;
        }

        private string ResolveProjectType( )
        {
            return File.ReadLines(ProjectPath).Any(line => line.Contains("csproj")) ? "x86" : "Win32";
        }

        public byte GetBitFieldConfig( )
        {
            byte field = 0;
            if ( X86D )
                field |= 1;
            if ( X86R )
                field |= 1 << 1;
            if ( X64D )
                field |= 1 << 2;
            if ( X64R )
                field |= 1 <<3;
            return field;
        }
    }



    public class Stats
    {
        public double Mu { get; private set; }
        public double Sigma { get; private set; }
        public double Dispersion
        {
            get { return Sigma*Sigma; }
            private set { }
        }

        public void Calculate( List<long> values )
        {
            if ( values.Count == 0 )
                return;
            Mu = values.Average();
            double temp = values.Sum(arg => Math.Pow(arg - Mu, 2));
            Sigma = values.Count > 1 ? Math.Sqrt(temp / ( values.Count - 1 )) : 0;
        }


    }

    public static class Logger
    {
        static StreamWriter _sw = new StreamWriter("log.txt", true);

        public static void ClearLog()
        {
            if (!File.Exists("log.txt"))
                return;
            
            _sw.Close();
            File.Delete("log.txt");
        }

        public static void Log(string logMessage)
        {
            _sw.WriteLine("{0} : {1}", DateTime.Now, logMessage);
        }
    }

    public class CfgMan
    {
        public List<Project> Prjcfg = new List<Project>();
        public TfsAccount Tfscfg = new TfsAccount();
        private static readonly string CurrentDir = Directory.GetCurrentDirectory();

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

        private void Serialize<T>( T cfg, string path )
        {
            try
            {
                using ( FileStream fs = new FileStream(CurrentDir + @"\" + path, FileMode.Create) )
                {
                    XmlWriterSettings xmlSettings = new XmlWriterSettings {Indent = true};
                    XmlWriter writer = XmlWriter.Create(fs, xmlSettings);
                    XmlSerializer ser = new XmlSerializer(typeof(T));
                    ser.Serialize(writer, cfg);
                }
            }
            catch ( Exception ex )
            {
                System.Windows.MessageBox.Show("Error during saving config: " + ex.Message);
            }
        }
        private void Deserialize<T>( ref T cfg, string path )
        {
            if ( !File.Exists(CurrentDir + @"\" + path) )
                return;
            try
            {
                using ( FileStream fs = new FileStream(CurrentDir + @"\" + path, FileMode.OpenOrCreate) )
                {
                    XmlReader reader = XmlReader.Create(fs);
                    XmlSerializer ser = new XmlSerializer(typeof(T));
                    cfg = (T)ser.Deserialize(reader);
                }
            }
            catch ( Exception ex )
            {
                System.Windows.MessageBox.Show("Error during loading config: " + ex.Message);
            }
        }
    }
}
