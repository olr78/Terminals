package net.terminals.companion.core

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Fixtures were created by the desktop Terminals 4.2.12 code (EncryptedExportFile.WriteEncrypted)
 * and by the legacy key derivation of Terminals <= 4.2.11, see Android/README.md.
 */
class ExportCryptoTest {
    private val password = "Пароль ü 1".toCharArray()

    private fun resource(name: String): String =
        javaClass.classLoader!!.getResource(name)!!.readText(Charsets.UTF_8)

    @Test
    fun currentFormat_Decrypt_ReturnsDesktopXml() {
        val export = ExportCrypto.parse(resource("export-v4212.xml"))
        assertEquals(resource("plain-favorites.xml"), ExportCrypto.decrypt(export, password.copyOf()))
    }

    @Test
    fun legacyFormat_Decrypt_ReturnsDesktopXml() {
        val export = ExportCrypto.parse(resource("export-legacy.xml"))
        assertEquals(resource("plain-favorites.xml"), ExportCrypto.decrypt(export, password.copyOf()))
    }

    @Test(expected = WrongPasswordException::class)
    fun currentFormat_WrongPassword_Throws() {
        ExportCrypto.decrypt(ExportCrypto.parse(resource("export-v4212.xml")), "Пароль ü 2".toCharArray())
    }

    @Test(expected = WrongPasswordException::class)
    fun legacyFormat_WrongPassword_Throws() {
        ExportCrypto.decrypt(ExportCrypto.parse(resource("export-legacy.xml")), "wrong".toCharArray())
    }

    @Test
    fun otherDocuments_IsEncryptedExport_False() {
        assertTrue(ExportCrypto.isEncryptedExport(resource("export-v4212.xml")))
        assertFalse(ExportCrypto.isEncryptedExport(resource("plain-favorites.xml")))
        assertFalse(ExportCrypto.isEncryptedExport("not xml"))
    }

    @Test
    fun rfc7914Vector_Pbkdf2Sha256_Matches() {
        val key = ExportCrypto.pbkdf2("PBKDF2WithHmacSHA256", "Password".toCharArray(), "NaCl".toByteArray(), 80000)
        assertArrayEquals(hex("4ddcd8f60b98be21830cee5ef22701f9641a4418d04c0414aeff08876b34ab56"), key)
    }

    private fun hex(text: String) = ByteArray(text.length / 2) { text.substring(it * 2, it * 2 + 2).toInt(16).toByte() }
}
