using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Connections;

namespace Tests.Connections
{
    /// <summary>
    /// Conversion of the password to the scan codes typed into the remote session using US keyboard layout.
    /// </summary>
    [TestClass]
    public class PasswordTyperTests
    {
        private IntPtr usLayout;

        [TestInitialize]
        public void LoadLayout()
        {
            this.usLayout = PasswordTyper.LoadUsLayout();
            Assert.AreNotEqual(IntPtr.Zero, this.usLayout, "US keyboard layout is not available");
        }

        [TestMethod]
        public void LettersDigitsAndSymbols_CreateKeys_UsesShiftForUpperCaseAndSymbols()
        {
            List<PasswordTyper.KeyEvent> keys;
            bool created = PasswordTyper.TryCreateKeys("aB1!", this.usLayout, false, out keys);

            Assert.IsTrue(created);
            // a, Shift+b, 1, Shift+1, Enter
            Assert.AreEqual("1Ed 1Eu 2Ad 30d 30u 2Au 02d 02u 2Ad 02d 02u 2Au 1Cd 1Cu", Format(keys));
        }

        [TestMethod]
        public void CapsLockOn_CreateKeys_InvertsShiftForLettersOnly()
        {
            List<PasswordTyper.KeyEvent> keys;
            PasswordTyper.TryCreateKeys("aB1", this.usLayout, true, out keys);

            // Shift+a gives "a" with caps lock, b without shift gives "B", digits are not affected
            Assert.AreEqual("2Ad 1Ed 1Eu 2Au 30d 30u 02d 02u 1Cd 1Cu", Format(keys));
        }

        [TestMethod]
        public void CharacterMissingInLayout_CreateKeys_ReturnsFalse()
        {
            List<PasswordTyper.KeyEvent> keys;
            Assert.IsFalse(PasswordTyper.TryCreateKeys("пароль", this.usLayout, false, out keys));
            Assert.IsFalse(PasswordTyper.TryCreateKeys(string.Empty, this.usLayout, false, out keys));
        }

        [TestMethod]
        public void AsciiPassword_ResolveLayout_FindsLayoutAbleToTypeIt()
        {
            IntPtr layout;
            string layoutToAnnounce;
            bool resolved = PasswordTyper.TryResolveLayout("Secret#2026", out layout, out layoutToAnnounce);

            Assert.IsTrue(resolved);
            Assert.AreNotEqual(IntPtr.Zero, layout);
            Assert.IsTrue(layoutToAnnounce == null || layoutToAnnounce == PasswordTyper.US_LAYOUT);
        }

        [TestMethod]
        public void UserWithDomain_GetUserWithoutDomain_ReturnsUserPart()
        {
            Assert.AreEqual("HmaraAdm", CredentialPromptFiller.GetUserWithoutDomain("HMARA" + (char)92 + "HmaraAdm"));
            Assert.AreEqual("HmaraAdm", CredentialPromptFiller.GetUserWithoutDomain("HmaraAdm@hmara.eu"));
            Assert.AreEqual("HmaraAdm", CredentialPromptFiller.GetUserWithoutDomain(" HmaraAdm "));
            Assert.IsNull(CredentialPromptFiller.GetUserWithoutDomain("ab"), "Too short names would match unrelated texts");
            Assert.IsNull(CredentialPromptFiller.GetUserWithoutDomain(null));
        }

        private static string Format(IEnumerable<PasswordTyper.KeyEvent> keys)
        {
            return string.Join(" ", keys.Select(key => key.ToString()));
        }
    }
}
