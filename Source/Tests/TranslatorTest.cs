using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Localization;

namespace Tests
{
    /// <summary>
    /// Lookup rules of the user interface translation dictionary.
    /// </summary>
    [TestClass]
    public class TranslatorTest
    {
        private const string DICTIONARY = "# comment\n" +
                                          "&File\t&Soubor\n" +
                                          "Save && Close\tUlozit a zavrit\n" +
                                          "Connect To\tPripojit k\n" +
                                          "Line one\\nLine two\tRadek jedna\\nRadek dva\n" +
                                          "Pending items:{0}\tCeka: {0}\n" +
                                          "Copy {0} to {1}, {0} again\tKopie {1} z {0}, {0} znovu\n" +
                                          "Published {0:d}\tPublikovano {0:d}\n";

        [TestInitialize]
        public void LoadDictionary()
        {
            Translator.Load("xx", new StringReader(DICTIONARY));
        }

        [TestCleanup]
        public void UnloadDictionary()
        {
            Translator.Load(string.Empty, null);
        }

        [TestMethod]
        public void ExactText_Translate_ReturnsTranslation()
        {
            Assert.AreEqual("&Soubor", Translator.T("&File"));
            Assert.AreEqual("Ulozit a zavrit", Translator.T("Save && Close"));
        }

        [TestMethod]
        public void UnknownText_Translate_ReturnsSameText()
        {
            Assert.AreEqual("My server", Translator.T("My server"));
            Assert.AreEqual(string.Empty, Translator.T(string.Empty));
            Assert.IsNull(Translator.T(null));
        }

        [TestMethod]
        public void DifferentMnemonicOrCase_Translate_UsesTextWithoutMnemonic()
        {
            Assert.AreEqual("Soubor", Translator.T("File"));
            Assert.AreEqual("Soubor", Translator.T("Fil&e"));
            Assert.AreEqual("Pripojit k", Translator.T("Connect to"));
        }

        [TestMethod]
        public void KnownTextWithSuffixOrSpaces_Translate_KeepsSuffixAndSpaces()
        {
            Assert.AreEqual("Pripojit k:", Translator.T("Connect To:"));
            Assert.AreEqual("Pripojit k...", Translator.T("Connect To..."));
            Assert.AreEqual("  Pripojit k ", Translator.T("  Connect To "));
        }

        [TestMethod]
        public void BothNewLineStyles_Translate_KeepsNewLineStyleOfSource()
        {
            Assert.AreEqual("Radek jedna\r\nRadek dva", Translator.T("Line one\r\nLine two"));
            Assert.AreEqual("Radek jedna\nRadek dva", Translator.T("Line one\nLine two"));
        }

        [TestMethod]
        public void FormattedText_Translate_ReplacesPlaceholdersByValues()
        {
            Assert.AreEqual("Ceka: 15", Translator.T("Pending items:15"));
            Assert.AreEqual("Kopie b z a, a znovu", Translator.T("Copy a to b, a again"));
            Assert.AreEqual("Copy a to b, c again", Translator.T("Copy a to b, c again"), "Repeated placeholder has to match the same value");
        }

        [TestMethod]
        public void Template_Format_TranslatesTemplateBeforeFormatting()
        {
            Assert.AreEqual("Ceka: 3", Translator.Format("Pending items:{0}", 3));
            Assert.AreEqual("Publikovano x", Translator.Format("Published {0:d}", "x"));
        }

        [TestMethod]
        public void NoLanguage_Translate_ReturnsSameText()
        {
            Translator.Load(string.Empty, null);
            Assert.IsFalse(Translator.IsActive);
            Assert.AreEqual("&File", Translator.T("&File"));
        }

        [TestMethod]
        public void ConfiguredOrWindowsLanguage_Resolve_ReturnsSupportedLanguage()
        {
            var ukrainian = new System.Globalization.CultureInfo("uk-UA");
            var english = new System.Globalization.CultureInfo("en-US");
            Assert.AreEqual(Translator.UKRAINIAN, Translator.ResolveLanguage(Translator.AUTOMATIC, ukrainian));
            Assert.AreEqual(Translator.ENGLISH, Translator.ResolveLanguage(Translator.AUTOMATIC, english));
            Assert.AreEqual(Translator.ENGLISH, Translator.ResolveLanguage(Translator.ENGLISH, ukrainian));
            Assert.AreEqual(Translator.UKRAINIAN, Translator.ResolveLanguage(Translator.UKRAINIAN, english));
        }

        [TestMethod]
        public void UkrainianDictionary_Initialize_TranslatesApplicationTexts()
        {
            Translator.Initialize(Translator.UKRAINIAN);

            Assert.AreEqual(Translator.UKRAINIAN, Translator.Language);
            Assert.AreEqual("&\u0424\u0430\u0439\u043b", Translator.T("&File"));
            Assert.AreEqual("\u041f\u0430\u0440\u0430\u043c\u0435\u0442\u0440\u0438", Translator.T("Options"));
            Assert.AreEqual("\u0417\u0431\u0435\u0440\u0435\u0433\u0442\u0438 \u0439 \u0437\u0430\u043a\u0440\u0438\u0442\u0438", Translator.T("Save && Close"));
            Assert.AreEqual("\u0423 \u0432\u0430\u0441 \u043e\u0441\u0442\u0430\u043d\u043d\u044f \u0432\u0435\u0440\u0441\u0456\u044f 4.1.2.", Translator.T("You are using the latest version 4.1.2."));
            Assert.AreEqual("\u041d\u0435 \u0432\u0434\u0430\u043b\u043e\u0441\u044f \u0437\u0430\u0441\u0442\u043e\u0441\u0443\u0432\u0430\u0442\u0438 \u043e\u043d\u043e\u0432\u043b\u0435\u043d\u043d\u044f: disk full", Translator.T("Unable to apply the update: disk full"));
        }
    }
}
