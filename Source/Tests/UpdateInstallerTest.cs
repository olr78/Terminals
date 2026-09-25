using System;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Updates;

namespace Tests
{
    /// <summary>
    /// Verification and extraction of the downloaded release package.
    /// </summary>
    [TestClass]
    public class UpdateInstallerTest
    {
        private string workDirectory;

        [TestInitialize]
        public void CreateWorkDirectory()
        {
            this.workDirectory = Path.Combine(Path.GetTempPath(), "TerminalsUpdateTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.workDirectory);
        }

        [TestCleanup]
        public void DeleteWorkDirectory()
        {
            Directory.Delete(this.workDirectory, true);
        }

        [TestMethod]
        public void MatchingChecksum_Verify_Passes()
        {
            string file = this.CreateFile("abc");
            // sha256 of "abc"
            var asset = new ReleaseAsset { Size = 3, Digest = "sha256:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad" };

            UpdateInstaller.VerifyChecksum(file, asset);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidDataException))]
        public void DifferentChecksum_Verify_Throws()
        {
            string file = this.CreateFile("abd");
            var asset = new ReleaseAsset { Size = 3, Digest = "sha256:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad" };

            UpdateInstaller.VerifyChecksum(file, asset);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidDataException))]
        public void DifferentSize_Verify_Throws()
        {
            string file = this.CreateFile("abc");
            var asset = new ReleaseAsset { Size = 4 };

            UpdateInstaller.VerifyChecksum(file, asset);
        }

        [TestMethod]
        public void ValidPackage_Extract_CreatesFilesIncludingSubdirectories()
        {
            string zipPath = this.CreatePackage("Terminals.exe", @"Plugins\Rdp\Terminals.Plugins.Rdp.dll");
            string target = Path.Combine(this.workDirectory, "files");

            UpdateInstaller.ExtractPackage(zipPath, target);

            Assert.IsTrue(File.Exists(Path.Combine(target, "Terminals.exe")));
            Assert.IsTrue(File.Exists(Path.Combine(target, @"Plugins\Rdp\Terminals.Plugins.Rdp.dll")));
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidDataException))]
        public void PathOutsideTarget_Extract_Throws()
        {
            string zipPath = this.CreatePackage("Terminals.exe", @"..\evil.dll");
            UpdateInstaller.ExtractPackage(zipPath, Path.Combine(this.workDirectory, "files"));
        }

        private string CreateFile(string content)
        {
            string file = Path.Combine(this.workDirectory, "package.bin");
            File.WriteAllText(file, content);
            return file;
        }

        private string CreatePackage(params string[] entries)
        {
            string zipPath = Path.Combine(this.workDirectory, "package.zip");
            using (var zip = new ZipOutputStream(File.Create(zipPath)))
            {
                foreach (string entryName in entries)
                {
                    // ZipEntry would normalize the backslashes, the portable package created by PowerShell keeps them
                    var entry = new ZipEntry(entryName);
                    zip.PutNextEntry(entry);
                    zip.Write(new byte[] { 1, 2, 3 }, 0, 3);
                }
            }

            return zipPath;
        }
    }
}
