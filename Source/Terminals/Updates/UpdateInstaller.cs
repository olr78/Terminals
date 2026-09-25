using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using ICSharpCode.SharpZipLib.Zip;

namespace Terminals.Updates
{
    internal enum InstallationType
    {
        /// <summary>
        /// Application directory is writable, update replaces the files from portable zip package.
        /// </summary>
        Portable,

        /// <summary>
        /// Installed by Windows installer package, update runs new msi package.
        /// </summary>
        Msi,

        /// <summary>
        /// Application directory isn't writable and not installed by msi.
        /// </summary>
        NotSupported
    }

    /// <summary>
    /// Downloads, verifies and applies the release package.
    /// The files can't be replaced while the application is running, so the update itself
    /// is performed by generated script, which waits until this process exits.
    /// </summary>
    internal class UpdateInstaller
    {
        /// <summary>
        /// Guid of the "TerminalsExe" component from the TerminalsSetup/Components.wxs
        /// </summary>
        private const string TERMINALS_EXE_COMPONENT = "{31F8007A-749F-4A74-AB3B-1F80813CEA32}";

        private const int INSTALLSTATE_LOCAL = 3;

        /// <summary>
        /// Files the user may customize, the portable update keeps the current version.
        /// </summary>
        private static readonly string[] preservedFiles = new[] { "ToolStrip.settings.config", "Terminals.log4net.config" };

        private readonly string applicationDirectory;

        private readonly string updateRoot;

        internal InstallationType InstallationType { get; private set; }

        internal UpdateInstaller()
        {
            this.applicationDirectory = Program.Info.Location.TrimEnd(Path.DirectorySeparatorChar);
            this.updateRoot = Path.Combine(Path.GetTempPath(), "TerminalsUpdate");
            this.InstallationType = this.DetectInstallationType();
        }

        internal ReleaseAsset SelectAsset(Release release)
        {
            switch (this.InstallationType)
            {
                case InstallationType.Portable:
                    return release.FindAsset(".zip");
                case InstallationType.Msi:
                    return release.FindAsset(".msi");
                default:
                    return null;
            }
        }

        private InstallationType DetectInstallationType()
        {
            if (this.IsInstalledByMsi())
                return InstallationType.Msi;

            if (this.CanWriteToApplicationDirectory())
                return InstallationType.Portable;

            return InstallationType.NotSupported;
        }

        private bool IsInstalledByMsi()
        {
            try
            {
                var path = new StringBuilder(1024);
                int length = path.Capacity;
                int state = MsiLocateComponent(TERMINALS_EXE_COMPONENT, path, ref length);
                if (state != INSTALLSTATE_LOCAL)
                    return false;

                string installedDirectory = Path.GetDirectoryName(path.ToString());
                return string.Equals(Path.GetFullPath(installedDirectory).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(this.applicationDirectory), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception exception)
            {
                Logging.Info("Unable to detect msi installation", exception);
                return false;
            }
        }

        private bool CanWriteToApplicationDirectory()
        {
            try
            {
                string testFile = Path.Combine(this.applicationDirectory, "update_" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(testFile, string.Empty);
                File.Delete(testFile);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Creates new empty directory, where the package of given version is downloaded.
        /// </summary>
        internal string PrepareDownloadDirectory(Version version)
        {
            try
            {
                if (Directory.Exists(this.updateRoot))
                    Directory.Delete(this.updateRoot, true);
            }
            catch (Exception exception)
            {
                // previous update files may be still locked, not a problem, we use new directory
                Logging.Info("Unable to clean previous update files", exception);
            }

            string directory = Path.Combine(this.updateRoot, version + "_" + DateTime.Now.ToString("yyyyMMddHHmmss"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        /// <summary>
        /// Throws exception, if the downloaded file doesn't match the expected checksum.
        /// </summary>
        internal static void VerifyChecksum(string filePath, ReleaseAsset asset)
        {
            var fileInfo = new FileInfo(filePath);
            if (asset.Size > 0 && fileInfo.Length != asset.Size)
                throw new InvalidDataException(string.Format("Downloaded file size {0} doesn't match expected size {1}.", fileInfo.Length, asset.Size));

            string expected = asset.Sha256;
            if (expected == null)
            {
                Logging.Info("Release asset doesn't provide checksum, only the file size was verified: " + asset.Name);
                return;
            }

            string actual = ComputeSha256(filePath);
            if (actual != expected)
                throw new InvalidDataException(string.Format("Downloaded file checksum {0} doesn't match expected checksum {1}.", actual, expected));
        }

        internal static string ComputeSha256(string filePath)
        {
            using (var sha = new SHA256CryptoServiceProvider())
            using (FileStream stream = File.OpenRead(filePath))
            {
                byte[] hash = sha.ComputeHash(stream);
                return string.Concat(hash.Select(b => b.ToString("x2")));
            }
        }

        /// <summary>
        /// Prepares the downloaded package and starts the script, which applies the update after this process exits.
        /// Returns the started script process.
        /// </summary>
        internal Process Apply(string packagePath)
        {
            string workDirectory = Path.GetDirectoryName(packagePath);
            string script;
            if (this.InstallationType == InstallationType.Msi)
            {
                script = this.CreateMsiScript(packagePath);
            }
            else
            {
                string filesDirectory = Path.Combine(workDirectory, "files");
                ExtractPackage(packagePath, filesDirectory);
                KeepPortableSetting(filesDirectory);
                script = this.CreatePortableScript(filesDirectory);
            }

            string scriptPath = Path.Combine(workDirectory, "update.cmd");
            // cmd reads the script using the active code page, which is switched to UTF-8 as the first command
            File.WriteAllText(scriptPath, script, new UTF8Encoding(false));
            return StartScript(scriptPath);
        }

        internal static void ExtractPackage(string zipPath, string targetDirectory)
        {
            string targetRoot = Path.GetFullPath(targetDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (var zip = new ZipFile(zipPath))
            {
                foreach (ZipEntry entry in zip)
                {
                    if (!entry.IsFile)
                        continue;

                    string relativePath = entry.Name.Replace('/', Path.DirectorySeparatorChar);
                    string targetPath = Path.GetFullPath(Path.Combine(targetRoot, relativePath));
                    if (!targetPath.StartsWith(targetRoot, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Update package contains invalid file path: " + entry.Name);

                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                    using (Stream source = zip.GetInputStream(entry))
                    using (FileStream target = File.Create(targetPath))
                    {
                        source.CopyTo(target);
                    }
                }
            }

            if (!File.Exists(Path.Combine(targetDirectory, "Terminals.exe")))
                throw new InvalidDataException("Update package doesn't contain Terminals.exe");
        }

        /// <summary>
        /// The package always contains portable configuration, keep the current value.
        /// </summary>
        private static void KeepPortableSetting(string filesDirectory)
        {
            string configPath = Path.Combine(filesDirectory, "Terminals.exe.config");
            if (!File.Exists(configPath))
                return;

            XDocument config = XDocument.Load(configPath);
            XElement portableValue = config.Descendants("setting")
                .Where(setting => (string)setting.Attribute("name") == "Portable")
                .Select(setting => setting.Element("value"))
                .FirstOrDefault();

            if (portableValue != null)
            {
                portableValue.Value = Properties.Settings.Default.Portable.ToString();
                config.Save(configPath);
            }
        }

        private string CreatePortableScript(string filesDirectory)
        {
            string excluded = string.Join(" ", preservedFiles);
            string update = string.Format(
                "robocopy {0} {1} /E /R:10 /W:1 /NP /NDL /XF {2} >> \"%LOG%\"\r\n" +
                "if errorlevel 8 (echo Copy failed >> \"%LOG%\") else (echo Copy finished >> \"%LOG%\")\r\n",
                QuotePath(filesDirectory), QuotePath(this.applicationDirectory), excluded);
            return this.CreateScript(update);
        }

        private string CreateMsiScript(string msiPath)
        {
            string msiLog = Path.Combine(Path.GetDirectoryName(msiPath), "msi.log");
            string update = string.Format(
                "msiexec /i {0} /passive /norestart /l*v {1}\r\n" +
                "echo msiexec exit code %ERRORLEVEL% >> \"%LOG%\"\r\n",
                QuotePath(msiPath), QuotePath(msiLog));
            return this.CreateScript(update);
        }

        private string CreateScript(string updateCommands)
        {
            int processId = Process.GetCurrentProcess().Id;
            string exePath = Path.Combine(this.applicationDirectory, "Terminals.exe");
            var script = new StringBuilder();
            script.Append("@echo off\r\n");
            script.Append("chcp 65001 >nul\r\n");
            script.Append("set \"LOG=%~dp0update.log\"\r\n");
            script.Append("echo Terminals update started %DATE% %TIME% > \"%LOG%\"\r\n");
            script.Append("set /a WAITED=0\r\n");
            // wait max. 10 minutes for the application to exit, the user may cancel the close
            script.Append(":wait\r\n");
            script.AppendFormat("tasklist /FI \"PID eq {0}\" /NH 2>nul | find \" {0} \" >nul\r\n", processId);
            script.Append("if errorlevel 1 goto closed\r\n");
            script.Append("set /a WAITED+=1\r\n");
            script.Append("if %WAITED% GEQ 600 (echo Timeout waiting for Terminals to exit >> \"%LOG%\" & exit /b 1)\r\n");
            script.Append("ping -n 2 127.0.0.1 >nul\r\n");
            script.Append("goto wait\r\n");
            script.Append(":closed\r\n");
            script.Append(updateCommands);
            script.AppendFormat("start \"\" {0}\r\n", QuotePath(exePath));
            script.Append("exit /b 0\r\n");
            return script.ToString();
        }

        /// <summary>
        /// Trailing backslash would escape the closing quote, percent sign would be expanded by cmd.
        /// </summary>
        private static string QuotePath(string path)
        {
            string escaped = path.TrimEnd(Path.DirectorySeparatorChar).Replace("%", "%%");
            return "\"" + escaped + "\"";
        }

        private static Process StartScript(string scriptPath)
        {
            var startInfo = new ProcessStartInfo("cmd.exe", "/c \"\"" + scriptPath + "\"\"");
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.WorkingDirectory = Path.GetDirectoryName(scriptPath);
            return Process.Start(startInfo);
        }

        [DllImport("msi.dll", CharSet = CharSet.Unicode)]
        private static extern int MsiLocateComponent(string szComponent, StringBuilder lpPathBuf, ref int pcchBuf);
    }
}
