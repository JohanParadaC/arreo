using System;
using System.IO;

namespace Arreo.Core
{
    /// <summary>
    /// The saved game on disk: one JSON file in a folder (Application.persistentDataPath in the game).
    /// Saving writes a temporary file first, so a crash mid-save never leaves half a file behind.
    /// A file that can't be loaded is moved aside with its date in the name (kept, not deleted)
    /// and is no longer offered to continue.
    /// </summary>
    public sealed class SaveStore
    {
        public const string FileName = "arreo-save.json";
        private const string TempSuffix = ".tmp";

        public SaveStore(string folder)
        {
            Folder = folder;
            FilePath = Path.Combine(folder, FileName);
        }

        public string Folder { get; }
        public string FilePath { get; }
        public bool Exists => File.Exists(FilePath);

        public void Save(SaveData data)
        {
            Directory.CreateDirectory(Folder);
            data.savedAtUtc = DateTime.UtcNow.ToString("o");
            string temp = FilePath + TempSuffix;
            File.WriteAllText(temp, SaveSerializer.Write(data));
            if (File.Exists(FilePath))
                File.Replace(temp, FilePath, null);
            else
                File.Move(temp, FilePath);
        }

        public SaveLoadResult Load()
        {
            if (!Exists)
                return new SaveLoadResult(SaveLoadStatus.Missing);

            SaveLoadResult result;
            try
            {
                result = SaveSerializer.Read(File.ReadAllText(FilePath));
            }
            catch (IOException e)
            {
                result = new SaveLoadResult(SaveLoadStatus.Corrupt, problem: $"it could not be opened ({e.Message})");
            }
            return result.Status == SaveLoadStatus.Loaded ? result : result.WithBackup(KeepAside());
        }

        private string KeepAside()
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string backup = Path.Combine(Folder, $"arreo-save.bad-{stamp}.json");
            for (int n = 2; File.Exists(backup); n++)
                backup = Path.Combine(Folder, $"arreo-save.bad-{stamp}-{n}.json");
            File.Move(FilePath, backup);
            return backup;
        }
    }
}
