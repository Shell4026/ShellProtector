#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Shell.Protector.Tests.Unit
{
    public class SaltRegistryTests
    {
        string dir;
        string file;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "ShellProtectorSaltRegistryTests_" + Path.GetRandomFileName());
            file = Path.Combine(dir, "salts.txt");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }

        [Test]
        public void Register_WritesEachSaltOnceAsSaltParameterName()
        {
            string other = UserKey.GenerateSalt();

            SaltRegistry.Register(TestKeys.Salt, file);
            SaltRegistry.Register(other, file);
            SaltRegistry.Register(TestKeys.Salt, file);

            Assert.That(File.ReadAllLines(file), Is.EqualTo(new[] { "SP_SALT_" + TestKeys.Salt, "SP_SALT_" + other }));
        }

        [Test]
        public void Register_RejectsInvalidSalt()
        {
            Assert.That(() => SaltRegistry.Register("not a salt", file), Throws.ArgumentException);
            Assert.That(File.Exists(file), Is.False);
        }

        [Test]
        public void GetDefaultFile_IsWhereTheOSCAppLooks()
        {
            string baseDir = Application.platform == RuntimePlatform.WindowsEditor
                ? "LocalLow"
                : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string expected = Path.Combine(baseDir, "ShellProtector", "salts.txt");
            Assert.That(SaltRegistry.GetDefaultFile(), Does.EndWith(expected));
        }
    }
}
#endif
