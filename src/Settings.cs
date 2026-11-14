using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace GravadorDeTela
{
    /// <summary>Preferências salvas em %APPDATA%\GravadorDeTela\config.ini (chave=valor).</summary>
    class Settings
    {
        public bool Mic = true;
        public string MicId = "";
        public bool SystemAudio = true;
        public bool HideTaskbar = false;
        public bool AreaMode = false;
        public Rectangle Area = Rectangle.Empty;
        public string Folder = DefaultFolder();

        static string FilePath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GravadorDeTela", "config.ini");
        }

        public static string DefaultFolder()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "Gravações de Tela");
        }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                string path = FilePath();
                if (!File.Exists(path)) return s;
                Dictionary<string, string> map = new Dictionary<string, string>();
                foreach (string line in File.ReadAllLines(path))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) map[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
                string v;
                if (map.TryGetValue("mic", out v)) s.Mic = v == "1";
                if (map.TryGetValue("micId", out v)) s.MicId = v;
                if (map.TryGetValue("systemAudio", out v)) s.SystemAudio = v == "1";
                if (map.TryGetValue("hideTaskbar", out v)) s.HideTaskbar = v == "1";
                if (map.TryGetValue("areaMode", out v)) s.AreaMode = v == "1";
                if (map.TryGetValue("folder", out v) && v.Length > 0) s.Folder = v;
                if (map.TryGetValue("area", out v))
                {
                    string[] p = v.Split(',');
                    if (p.Length == 4)
                        s.Area = new Rectangle(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]));
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                string path = FilePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, new string[] {
                    "mic=" + (Mic ? "1" : "0"),
                    "micId=" + MicId,
                    "systemAudio=" + (SystemAudio ? "1" : "0"),
                    "hideTaskbar=" + (HideTaskbar ? "1" : "0"),
                    "areaMode=" + (AreaMode ? "1" : "0"),
                    "area=" + Area.X + "," + Area.Y + "," + Area.Width + "," + Area.Height,
                    "folder=" + Folder
                });
            }
            catch { }
        }
    }
}
