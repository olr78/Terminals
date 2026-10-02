using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Terminals.Localization;

namespace Terminals.Updates
{
    /// <summary>
    /// Generates PowerShell script, which applies the update after Terminals exits and shows its progress in a window.
    /// The script is created from embedded template UpdateProgress.ps1.
    /// </summary>
    internal class UpdateProgressScript
    {
        private const string TEMPLATE = "Terminals.Updates.UpdateProgress.ps1";

        private readonly Dictionary<string, string> values = new Dictionary<string, string>();

        internal UpdateProgressScript(string applicationDirectory, Version version, string[] preservedFiles)
        {
            this.values["PROCESS_ID"] = Process.GetCurrentProcess().Id.ToString();
            this.values["TARGET_DIRECTORY"] = ToLiteral(applicationDirectory);
            this.values["TERMINALS_EXE"] = ToLiteral(Path.Combine(applicationDirectory, "Terminals.exe"));
            this.values["PRESERVED_FILES"] = "@(" + string.Join(", ", preservedFiles.Select(ToLiteral)) + ")";
            this.values["TEXT_TITLE"] = ToLiteral(Translator.T("Terminals Update"));
            this.values["TEXT_HEADER"] = ToLiteral(Translator.Format("Updating Terminals to version {0}", version));
            this.values["TEXT_WAITING"] = ToLiteral(Translator.T("Waiting for Terminals to close..."));
            this.values["TEXT_COPYING"] = ToLiteral(Translator.T("Copying files ({0} of {1})..."));
            this.values["TEXT_INSTALLING"] = ToLiteral(Translator.T("Installing the new version..."));
            this.values["TEXT_STARTING"] = ToLiteral(Translator.T("Starting Terminals..."));
            this.values["TEXT_COPY_FAILED"] = ToLiteral(Translator.T("The update wasn't completed, {0} files could not be copied. See the log file {1}"));
            this.values["TEXT_INSTALL_FAILED"] = ToLiteral(Translator.T("Installation of the new version failed (exit code {0}). See the log file {1}"));
            this.SetPortableInstallation(string.Empty);
        }

        internal void SetPortableInstallation(string filesDirectory)
        {
            this.values["MODE"] = ToLiteral(InstallationType.Portable.ToString());
            this.values["SOURCE_DIRECTORY"] = ToLiteral(filesDirectory);
            this.values["MSI_ARGUMENTS"] = ToLiteral(string.Empty);
        }

        internal void SetMsiInstallation(string msiArguments)
        {
            this.values["MODE"] = ToLiteral(InstallationType.Msi.ToString());
            this.values["SOURCE_DIRECTORY"] = ToLiteral(string.Empty);
            this.values["MSI_ARGUMENTS"] = ToLiteral(msiArguments);
        }

        /// <summary>
        /// Writes the script to given path.
        /// </summary>
        /// <param name="scriptPath">Target path of the generated script.</param>
        /// <param name="fallbackScript">Script without progress, which is used, when this script fails.</param>
        /// <param name="logFile">Log file shared with the fallback script.</param>
        internal void Write(string scriptPath, string fallbackScript, string logFile)
        {
            this.values["FALLBACK_SCRIPT"] = ToLiteral(fallbackScript);
            this.values["LOG_FILE"] = ToLiteral(logFile);
            // PowerShell reads files without byte order mark using the ANSI code page
            File.WriteAllText(scriptPath, this.Generate(), new UTF8Encoding(true));
        }

        internal string Generate()
        {
            var script = new StringBuilder(ReadTemplate());
            foreach (KeyValuePair<string, string> value in this.values)
            {
                script.Replace("__" + value.Key + "__", value.Value);
            }

            return script.ToString();
        }

        private static string ReadTemplate()
        {
            using (Stream stream = typeof(UpdateProgressScript).Assembly.GetManifestResourceStream(TEMPLATE))
            {
                if (stream == null)
                    throw new InvalidOperationException("Missing update script template " + TEMPLATE);

                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Converts the value to single quoted PowerShell string. PowerShell accepts also typographic
        /// single quotes as the string delimiters (e.g. apostrophe in translations), all of them are escaped by doubling.
        /// </summary>
        internal static string ToLiteral(string value)
        {
            var literal = new StringBuilder("'");
            foreach (char character in value)
            {
                literal.Append(character);
                if (IsSingleQuote(character))
                    literal.Append(character);
            }

            return literal.Append('\'').ToString();
        }

        private static bool IsSingleQuote(char character)
        {
            return character == '\'' || character == '‘' || character == '’' ||
                   character == '‚' || character == '‛';
        }
    }
}
