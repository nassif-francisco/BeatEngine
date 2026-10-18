using System;
using System.IO;

namespace BeatEngine.Core.Game
{
    public enum TileColor { Multicolor, Crema, Blanco, Gris, Amarillo, Verde }

    public sealed class PlayerPreferences
    {
        public TileColor TileColor { get; set; } = TileColor.Multicolor;
        private static string SavePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BeatEngine", "preferences.txt");

        public static PlayerPreferences Load()
        {
            var preferences = new PlayerPreferences();
            try
            {
                if (File.Exists(SavePath) && Enum.TryParse<TileColor>(File.ReadAllText(SavePath), out var color)
                    && Enum.IsDefined(typeof(TileColor), color))
                    preferences.TileColor = color;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return preferences;
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            File.WriteAllText(SavePath, TileColor.ToString());
        }
    }
}
