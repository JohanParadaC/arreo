using System.IO;
using Arreo.Voice;
using NUnit.Framework;

namespace Arreo.Tests.EditMode
{
    public class VoiceProfileStoreTests
    {
        private string _directory;

        [SetUp]
        public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "ArreoVoiceProfileTests", Path.GetRandomFileName());

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        [Test]
        public void NewProfile_HasNoCanto()
        {
            var store = new VoiceProfileStore(_directory);

            Assert.That(store.HasCanto, Is.False);
            Assert.That(store.TryLoadCanto(out byte[] wav), Is.False);
            Assert.That(wav, Is.Null);
        }

        [Test]
        public void SavedCanto_LoadsBackTheSameBytes()
        {
            var store = new VoiceProfileStore(_directory);
            byte[] canto = WavCodec.Encode(new float[16000 * 6], 16000);

            store.SaveCanto(canto);

            Assert.That(store.HasCanto, Is.True);
            Assert.That(new VoiceProfileStore(_directory).TryLoadCanto(out byte[] loaded), Is.True);
            Assert.That(loaded, Is.EqualTo(canto));
            Assert.That(Path.GetFileName(store.CantoPath), Is.EqualTo(VoiceProfileStore.CantoFileName));
        }

        [Test]
        public void SavingAgain_ReplacesTheCanto()
        {
            var store = new VoiceProfileStore(_directory);
            store.SaveCanto(new byte[] { 1, 2, 3 });

            store.SaveCanto(new byte[] { 4, 5 });

            store.TryLoadCanto(out byte[] loaded);
            Assert.That(loaded, Is.EqualTo(new byte[] { 4, 5 }));
            Assert.That(Directory.GetFiles(_directory), Has.Length.EqualTo(1), "no temporary file left behind");
        }

        [Test]
        public void DeletedCanto_IsGone()
        {
            var store = new VoiceProfileStore(_directory);
            store.SaveCanto(new byte[] { 1 });

            store.DeleteCanto();
            store.DeleteCanto();

            Assert.That(store.HasCanto, Is.False);
        }

        [Test]
        public void Prefs_DefaultUntilSaved_ThenRoundTrip()
        {
            var store = new VoiceProfileStore(_directory);

            VoicePrefs defaults = store.LoadPrefs();
            Assert.That(defaults.muteOtherCantos, Is.False);
            Assert.That(defaults.microphone, Is.Empty);

            store.SavePrefs(new VoicePrefs { muteOtherCantos = true, microphone = "Micrófono (Realtek)" });
            VoicePrefs loaded = new VoiceProfileStore(_directory).LoadPrefs();

            Assert.That(loaded.muteOtherCantos, Is.True);
            Assert.That(loaded.microphone, Is.EqualTo("Micrófono (Realtek)"));
            Assert.That(loaded.version, Is.EqualTo(VoicePrefs.CurrentVersion));
        }

        [Test]
        public void BrokenPrefs_GiveTheDefaultsInsteadOfThrowing()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory, VoiceProfileStore.PrefsFileName), "{ this is not json");

            VoicePrefs prefs = new VoiceProfileStore(_directory).LoadPrefs();

            Assert.That(prefs.muteOtherCantos, Is.False);
            Assert.That(prefs.microphone, Is.Empty);
        }
    }
}
