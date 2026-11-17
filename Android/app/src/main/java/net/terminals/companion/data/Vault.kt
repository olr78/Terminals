package net.terminals.companion.data

import android.content.Context
import android.net.Uri
import java.io.File
import net.terminals.companion.core.AttemptThrottle
import net.terminals.companion.core.ExportCrypto
import net.terminals.companion.core.Favorite
import net.terminals.companion.core.FavoritesParser
import net.terminals.companion.core.InvalidExportException

/**
 * Keeps only the encrypted export (as exported by the desktop Terminals) in the app private storage.
 * Decrypted connections exist only in memory while the app is unlocked.
 */
class Vault(private val context: Context) {
    private val file = File(context.filesDir, "terminals-export.xml")

    private val preferences = context.getSharedPreferences("vault", Context.MODE_PRIVATE)

    val throttle = AttemptThrottle(object : AttemptThrottle.Store {
        override var failures: Int
            get() = preferences.getInt("failures", 0)
            set(value) = preferences.edit().putInt("failures", value).apply()

        override var lockedUntilMillis: Long
            get() = preferences.getLong("lockedUntil", 0)
            set(value) = preferences.edit().putLong("lockedUntil", value).apply()
    })

    val hasExport: Boolean
        get() = file.exists()

    val importedAtMillis: Long
        get() = preferences.getLong("importedAt", 0)

    /** Validates and stores the export. Doesn't need the password, the content stays encrypted. */
    fun import(uri: Uri) {
        val content = context.contentResolver.openInputStream(uri)?.use { it.readBytes() }
            ?: throw InvalidExportException("Unable to read the file")
        val xml = decodeXml(content)
        ExportCrypto.parse(xml)

        val temporary = File(context.filesDir, "terminals-export.tmp")
        temporary.writeText(xml, Charsets.UTF_8)
        if (!temporary.renameTo(file)) {
            file.delete()
            if (!temporary.renameTo(file))
                throw InvalidExportException("Unable to save the file")
        }
        preferences.edit().putLong("importedAt", System.currentTimeMillis()).apply()
    }

    /** Throws WrongPasswordException or InvalidExportException. Call outside of the main thread. */
    fun unlock(password: CharArray): List<Favorite> {
        val export = ExportCrypto.parse(file.readText(Charsets.UTF_8))
        val xml = ExportCrypto.decrypt(export, password)
        return FavoritesParser.parse(xml)
    }

    fun delete() {
        file.delete()
        preferences.edit().remove("importedAt").apply()
    }

    private fun decodeXml(content: ByteArray): String = when {
        content.size >= 2 && content[0] == 0xFF.toByte() && content[1] == 0xFE.toByte() ->
            String(content, 2, content.size - 2, Charsets.UTF_16LE)
        content.size >= 2 && content[0] == 0xFE.toByte() && content[1] == 0xFF.toByte() ->
            String(content, 2, content.size - 2, Charsets.UTF_16BE)
        content.size >= 3 && content[0] == 0xEF.toByte() && content[1] == 0xBB.toByte() && content[2] == 0xBF.toByte() ->
            String(content, 3, content.size - 3, Charsets.UTF_8)
        else -> String(content, Charsets.UTF_8)
    }
}
