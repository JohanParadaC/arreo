using System.IO;
using Arreo.Net;
using UnityEngine;

namespace Arreo.Tests.PlayMode
{
    /// <summary>
    /// Tests that host a game point its save at a temporary folder first: otherwise the host's
    /// autosave on stopping would write over the developer's real saved game.
    /// </summary>
    internal static class TestSaves
    {
        public static string NewFolder() => Path.Combine(Path.GetTempPath(), "ArreoTests", "saves", Path.GetRandomFileName());

        /// <summary>Makes the loaded scene's save use <paramref name="folder"/> (a new temporary one if null); returns it.</summary>
        public static string UseFolder(string folder = null)
        {
            folder ??= NewFolder();
            Object.FindFirstObjectByType<NetworkSave>().UseFolder(folder);
            return folder;
        }

        public static void Delete(string folder)
        {
            if (folder != null && Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }
}
