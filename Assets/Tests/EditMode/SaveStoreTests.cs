using System.IO;
using System.Linq;
using Arreo.Core;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    public class SaveStoreTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "ArreoSaveStoreTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        private static SaveData CreateSave(long money)
        {
            var save = new SaveData();
            save.money.hato = money;
            save.clock.day = 2;
            return save;
        }

        [Test]
        public void NoFile_IsMissing()
        {
            var store = new SaveStore(_directory);

            Assert.That(store.Exists, Is.False);
            Assert.That(store.Load().Status, Is.EqualTo(SaveLoadStatus.Missing));
        }

        [Test]
        public void Save_ThenLoad_RoundTrips()
        {
            var store = new SaveStore(_directory);

            store.Save(CreateSave(money: 3_000_000));
            SaveLoadResult result = store.Load();

            Assert.That(store.Exists, Is.True);
            Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.Loaded));
            Assert.That(result.Data.money.hato, Is.EqualTo(3_000_000));
            Assert.That(result.Data.clock.day, Is.EqualTo(2));
        }

        [Test]
        public void Save_ReplacesTheLastSave_AndLeavesNoTemporaryFile()
        {
            var store = new SaveStore(_directory);

            store.Save(CreateSave(money: 1));
            store.Save(CreateSave(money: 2));

            Assert.That(store.Load().Data.money.hato, Is.EqualTo(2));
            Assert.That(Directory.GetFiles(_directory).Select(Path.GetFileName), Is.EqualTo(new[] { SaveStore.FileName }));
        }

        [Test]
        public void Save_CreatesTheFolderIfNeeded()
        {
            var store = new SaveStore(Path.Combine(_directory, "nueva"));

            store.Save(CreateSave(money: 5));

            Assert.That(store.Exists, Is.True);
        }

        [Test]
        public void CorruptFile_IsKeptAsACopy_AndNoLongerOfferedToContinue()
        {
            var store = new SaveStore(_directory);
            File.WriteAllText(store.FilePath, "{\"version\":2,\"clock\":{\"da");

            SaveLoadResult result = store.Load();

            Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.Corrupt));
            Assert.That(result.BackupPath, Does.StartWith(_directory));
            Assert.That(File.ReadAllText(result.BackupPath), Is.EqualTo("{\"version\":2,\"clock\":{\"da"), "the copy must be the file as it was");
            Assert.That(store.Exists, Is.False, "a broken save must not be offered again");
        }

        [Test]
        public void SaveFromAnUnknownVersion_IsKeptAsACopy()
        {
            var store = new SaveStore(_directory);
            File.WriteAllText(store.FilePath, "{\"version\":99}");

            SaveLoadResult result = store.Load();

            Assert.That(result.Status, Is.EqualTo(SaveLoadStatus.UnknownVersion));
            Assert.That(File.Exists(result.BackupPath), Is.True);
            Assert.That(store.Exists, Is.False);
        }

        [Test]
        public void TwoBrokenFiles_KeepTwoCopies()
        {
            var store = new SaveStore(_directory);
            File.WriteAllText(store.FilePath, "roto 1");
            string first = store.Load().BackupPath;
            File.WriteAllText(store.FilePath, "roto 2");
            string second = store.Load().BackupPath;

            Assert.That(second, Is.Not.EqualTo(first));
            Assert.That(File.ReadAllText(first), Is.EqualTo("roto 1"));
            Assert.That(File.ReadAllText(second), Is.EqualTo("roto 2"));
        }
    }
}
