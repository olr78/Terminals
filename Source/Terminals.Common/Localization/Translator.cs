using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Terminals.Localization
{
    /// <summary>
    /// Translates user interface texts using dictionary of english source text and its translation.
    /// The dictionary is embedded resource "Translations_{language}.txt".
    /// If no language is active, all texts are returned unchanged.
    /// </summary>
    public static class Translator
    {
        public const string AUTOMATIC = "";

        public const string ENGLISH = "en";

        public const string UKRAINIAN = "uk";

        private const int MAX_CACHE_SIZE = 5000;

        private static readonly string[] suffixes = new[] { "...", " ...", ":", " :", "!", ".", "?" };

        private static readonly Regex placeholder = new Regex(@"\{(\d+)(:[^}]*)?\}", RegexOptions.Compiled);

        private static readonly object syncRoot = new object();

        private static Dictionary<string, string> exact = new Dictionary<string, string>(StringComparer.Ordinal);

        private static Dictionary<string, string> ignoreCase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, string> withoutMnemonic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static List<Pattern> patterns = new List<Pattern>();

        private static readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Gets the language code of active translation, or empty string, if texts aren't translated.
        /// </summary>
        public static string Language { get; private set; }

        public static bool IsActive
        {
            get { return !string.IsNullOrEmpty(Language); }
        }

        static Translator()
        {
            Language = string.Empty;
        }

        /// <summary>
        /// Resolves the language to use from configured value.
        /// Empty value means automatic detection by the Windows user interface language.
        /// </summary>
        public static string ResolveLanguage(string configured, CultureInfo uiCulture)
        {
            if (string.Equals(configured, UKRAINIAN, StringComparison.OrdinalIgnoreCase))
                return UKRAINIAN;

            if (string.Equals(configured, ENGLISH, StringComparison.OrdinalIgnoreCase))
                return ENGLISH;

            return uiCulture.TwoLetterISOLanguageName == UKRAINIAN ? UKRAINIAN : ENGLISH;
        }

        /// <summary>
        /// Loads the translation for configured language. English is the source language, nothing is loaded.
        /// </summary>
        public static void Initialize(string configured)
        {
            string language = ResolveLanguage(configured, CultureInfo.CurrentUICulture);
            if (language == ENGLISH)
            {
                Load(string.Empty, null);
                return;
            }

            using (Stream stream = OpenDictionary(language))
            {
                if (stream == null)
                {
                    Load(string.Empty, null);
                    return;
                }

                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    Load(language, reader);
                }
            }
        }

        private static Stream OpenDictionary(string language)
        {
            Assembly assembly = typeof(Translator).Assembly;
            string suffix = ".Translations_" + language + ".txt";
            string resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            return resourceName == null ? null : assembly.GetManifestResourceStream(resourceName);
        }

        /// <summary>
        /// Loads the dictionary in format "source[TAB]translation" per line.
        /// Escape sequences: \n new line, \t tab, \\ backslash. Lines starting with # are comments.
        /// </summary>
        public static void Load(string language, TextReader reader)
        {
            var newExact = new Dictionary<string, string>(StringComparer.Ordinal);
            var newIgnoreCase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var newWithoutMnemonic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var newPatterns = new List<Pattern>();

            string line;
            while (reader != null && (line = reader.ReadLine()) != null)
            {
                if (line.Length == 0 || line[0] == '#')
                    continue;

                int separator = line.IndexOf('\t');
                if (separator <= 0)
                    continue;

                string source = Unescape(line.Substring(0, separator));
                string translation = Unescape(line.Substring(separator + 1));
                if (translation.Length == 0)
                    continue;

                newExact[source] = translation;
                if (!newIgnoreCase.ContainsKey(source))
                    newIgnoreCase[source] = translation;

                string sourceWithoutMnemonic = RemoveMnemonic(source);
                if (!newWithoutMnemonic.ContainsKey(sourceWithoutMnemonic))
                    newWithoutMnemonic[sourceWithoutMnemonic] = RemoveMnemonic(translation);

                if (placeholder.IsMatch(source))
                    newPatterns.Add(new Pattern(source, translation));
            }

            lock (syncRoot)
            {
                exact = newExact;
                ignoreCase = newIgnoreCase;
                withoutMnemonic = newWithoutMnemonic;
                patterns = newPatterns;
                cache.Clear();
                Language = reader == null ? string.Empty : language;
            }
        }

        /// <summary>
        /// Returns translated text, or the same text, if there is no translation.
        /// </summary>
        public static string T(string text)
        {
            if (!IsActive || string.IsNullOrEmpty(text))
                return text;

            lock (syncRoot)
            {
                string translated;
                if (cache.TryGetValue(text, out translated))
                    return translated;

                // dictionary keys use "\n", the texts may use both new line styles
                bool windowsNewLines = text.Contains("\r\n");
                string key = windowsNewLines ? text.Replace("\r\n", "\n") : text;
                translated = Lookup(key, true);
                if (translated == null)
                    translated = text;
                else if (windowsNewLines)
                    translated = translated.Replace("\n", "\r\n");

                if (cache.Count > MAX_CACHE_SIZE)
                    cache.Clear();

                cache[text] = translated;
                return translated;
            }
        }

        /// <summary>
        /// Translates the format template and formats it with the arguments.
        /// </summary>
        public static string Format(string template, params object[] args)
        {
            return string.Format(T(template), args);
        }

        private static string Lookup(string text, bool trySuffixes)
        {
            string translated;
            if (exact.TryGetValue(text, out translated))
                return translated;

            string trimmed = text.Trim();
            if (trimmed.Length == 0)
                return null;

            if (trimmed.Length != text.Length)
            {
                translated = Lookup(trimmed, trySuffixes);
                if (translated == null)
                    return null;

                int start = text.IndexOf(trimmed, StringComparison.Ordinal);
                return text.Substring(0, start) + translated + text.Substring(start + trimmed.Length);
            }

            if (ignoreCase.TryGetValue(text, out translated))
                return translated;

            if (withoutMnemonic.TryGetValue(RemoveMnemonic(text), out translated))
                return translated;

            if (trySuffixes)
            {
                foreach (string suffix in suffixes)
                {
                    if (text.Length <= suffix.Length || !text.EndsWith(suffix, StringComparison.Ordinal))
                        continue;

                    translated = Lookup(text.Substring(0, text.Length - suffix.Length), false);
                    if (translated != null)
                        return translated + suffix;
                }
            }

            foreach (Pattern pattern in patterns)
            {
                translated = pattern.Apply(text);
                if (translated != null)
                    return translated;
            }

            return null;
        }

        /// <summary>
        /// Removes single ampersands used as keyboard mnemonic, keeps escaped ampersand "&&" as "&".
        /// </summary>
        public static string RemoveMnemonic(string text)
        {
            if (text.IndexOf('&') < 0)
                return text;

            var result = new StringBuilder(text.Length);
            for (int index = 0; index < text.Length; index++)
            {
                char current = text[index];
                if (current == '&')
                {
                    if (index + 1 < text.Length && text[index + 1] == '&')
                    {
                        result.Append('&');
                        index++;
                    }

                    continue;
                }

                result.Append(current);
            }

            return result.ToString();
        }

        private static string Unescape(string text)
        {
            if (text.IndexOf('\\') < 0)
                return text;

            var result = new StringBuilder(text.Length);
            for (int index = 0; index < text.Length; index++)
            {
                char current = text[index];
                if (current == '\\' && index + 1 < text.Length)
                {
                    char next = text[++index];
                    switch (next)
                    {
                        case 'n':
                            result.Append('\n');
                            break;
                        case 't':
                            result.Append('\t');
                            break;
                        default:
                            result.Append(next);
                            break;
                    }

                    continue;
                }

                result.Append(current);
            }

            return result.ToString();
        }

        /// <summary>
        /// Translation of text created by string.Format, the placeholders capture the formatted values.
        /// </summary>
        private class Pattern
        {
            private readonly Regex regex;

            private readonly string translation;

            internal Pattern(string source, string translation)
            {
                this.translation = translation;
                var expression = new StringBuilder("^");
                var used = new HashSet<string>();
                int position = 0;
                foreach (Match match in placeholder.Matches(source))
                {
                    expression.Append(Regex.Escape(source.Substring(position, match.Index - position)));
                    string group = "p" + match.Groups[1].Value;
                    expression.Append(used.Add(group) ? "(?<" + group + ">.*?)" : @"\k<" + group + ">");
                    position = match.Index + match.Length;
                }

                expression.Append(Regex.Escape(source.Substring(position)));
                expression.Append('$');
                this.regex = new Regex(expression.ToString(), RegexOptions.Singleline);
            }

            internal string Apply(string text)
            {
                Match match = this.regex.Match(text);
                if (!match.Success)
                    return null;

                return placeholder.Replace(this.translation, argument =>
                {
                    Group captured = match.Groups["p" + argument.Groups[1].Value];
                    return captured.Success ? captured.Value : argument.Value;
                });
            }
        }
    }
}
