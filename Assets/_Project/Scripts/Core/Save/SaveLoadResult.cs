namespace Arreo.Core
{
    public enum SaveLoadStatus
    {
        /// <summary>There is no saved game.</summary>
        Missing,
        Loaded,
        /// <summary>The file isn't a readable save: truncated, edited by hand, not JSON, impossible values.</summary>
        Corrupt,
        /// <summary>Saved by a newer (or unknown) version of the game.</summary>
        UnknownVersion,
    }

    /// <summary>What reading a saved game gave.</summary>
    public sealed class SaveLoadResult
    {
        public SaveLoadResult(SaveLoadStatus status, SaveData data = null, int fileVersion = 0, string problem = "", string backupPath = "")
        {
            Status = status;
            Data = data;
            FileVersion = fileVersion;
            Problem = problem;
            BackupPath = backupPath;
        }

        public SaveLoadStatus Status { get; }

        /// <summary>The game, already migrated to the current version; null unless loaded.</summary>
        public SaveData Data { get; }

        /// <summary>The version the file was written with (0 if it couldn't be read).</summary>
        public int FileVersion { get; }

        /// <summary>What was wrong with the file, for the warning.</summary>
        public string Problem { get; }

        /// <summary>Where a broken file was kept, if it was.</summary>
        public string BackupPath { get; }

        public SaveLoadResult WithBackup(string backupPath) => new SaveLoadResult(Status, Data, FileVersion, Problem, backupPath);
    }
}
