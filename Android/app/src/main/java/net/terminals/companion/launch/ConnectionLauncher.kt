package net.terminals.companion.launch

import android.content.ActivityNotFoundException
import android.content.ClipData
import android.content.ClipDescription
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.Handler
import android.os.Looper
import android.os.PersistableBundle
import androidx.core.content.FileProvider
import java.io.File
import net.terminals.companion.R
import net.terminals.companion.core.Favorite
import net.terminals.companion.core.Protocols

/**
 * Opens the connection in an installed client app: Windows App / Remote Desktop (RDP),
 * any app handling ssh://, telnet://, vnc:// (JuiceSSH, Termius, RealVNC Viewer, ...) or the browser.
 */
class ConnectionLauncher(private val context: Context) {

    sealed class Result {
        object Started : Result()
        data class NoClient(val messageId: Int) : Result()
    }

    fun canConnect(favorite: Favorite): Boolean = when (favorite.protocol.uppercase()) {
        "RDP", "SSH", "TELNET", "VNC", "HTTP", "HTTPS" -> favorite.serverName.isNotEmpty() || favorite.url.isNotEmpty()
        else -> false
    }

    fun connect(favorite: Favorite): Result = when (favorite.protocol.uppercase()) {
        "RDP" -> connectRdp(favorite)
        "SSH" -> view(uri("ssh", favorite, withUser = true), R.string.no_ssh_client)
        "TELNET" -> view(uri("telnet", favorite, withUser = false), R.string.no_ssh_client)
        "VNC" -> view(uri("vnc", favorite, withUser = false), R.string.no_vnc_client)
        "HTTP", "HTTPS" -> view(webUri(favorite), R.string.no_browser)
        else -> Result.NoClient(R.string.protocol_not_supported)
    }

    private fun connectRdp(favorite: Favorite): Result {
        // .rdp file is supported by the Windows App / Remote Desktop client, rdp:// URI by older clients
        val file = writeRdpFile(favorite)
        val fileUri = FileProvider.getUriForFile(context, "${context.packageName}.files", file)
        val fileIntent = Intent(Intent.ACTION_VIEW)
            .setDataAndType(fileUri, "application/x-rdp")
            .addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION or Intent.FLAG_ACTIVITY_NEW_TASK)
        if (start(fileIntent))
            return Result.Started

        val query = buildString {
            append("full%20address=s:").append(Uri.encode(favorite.address))
            if (favorite.fullUserName.isNotEmpty())
                append("&username=s:").append(Uri.encode(favorite.fullUserName))
        }
        if (start(Intent(Intent.ACTION_VIEW, Uri.parse("rdp://$query")).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)))
            return Result.Started

        return Result.NoClient(R.string.no_rdp_client)
    }

    private fun writeRdpFile(favorite: Favorite): File {
        val directory = File(context.cacheDir, "rdp")
        directory.mkdirs()
        directory.listFiles()?.forEach { it.delete() }
        val safeName = favorite.name.replace(Regex("[^\\p{L}\\p{N}._ -]"), "_").take(60).ifBlank { "connection" }
        val file = File(directory, "$safeName.rdp")
        val lines = mutableListOf("full address:s:${favorite.address}")
        if (favorite.fullUserName.isNotEmpty())
            lines += "username:s:${favorite.fullUserName}"
        file.writeText(lines.joinToString("\r\n", postfix = "\r\n"), Charsets.UTF_8)
        return file
    }

    private fun uri(scheme: String, favorite: Favorite, withUser: Boolean): Uri {
        val user = if (withUser && favorite.userName.isNotEmpty()) Uri.encode(favorite.userName) + "@" else ""
        val port = if (favorite.port > 0) ":${favorite.port}" else ""
        return Uri.parse("$scheme://$user${favorite.serverName}$port")
    }

    private fun webUri(favorite: Favorite): Uri {
        val url = favorite.url.trim()
        if (url.isNotEmpty() && url != "http://" && url != "https://")
            return Uri.parse(url)
        val scheme = if (favorite.protocol.equals(Protocols.HTTPS, true)) "https" else "http"
        val port = if (favorite.port > 0 && favorite.port != Protocols.defaultPort(favorite.protocol)) ":${favorite.port}" else ""
        return Uri.parse("$scheme://${favorite.serverName}$port")
    }

    private fun view(uri: Uri, noClientMessage: Int): Result =
        if (start(Intent(Intent.ACTION_VIEW, uri).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))) Result.Started
        else Result.NoClient(noClientMessage)

    private fun start(intent: Intent): Boolean =
        try {
            context.startActivity(intent)
            true
        } catch (e: ActivityNotFoundException) {
            false
        }

    /** Copies the text marked as sensitive (hidden in the clipboard preview) and clears it after a minute. */
    fun copy(label: String, text: String, sensitive: Boolean) {
        val clipboard = context.getSystemService(ClipboardManager::class.java)
        val clip = ClipData.newPlainText(label, text)
        if (sensitive) {
            clip.description.extras = PersistableBundle().apply {
                if (Build.VERSION.SDK_INT >= 33)
                    putBoolean(ClipDescription.EXTRA_IS_SENSITIVE, true)
                else
                    putBoolean("android.content.extra.IS_SENSITIVE", true)
            }
            Handler(Looper.getMainLooper()).postDelayed({ clearIfUnchanged(clipboard, text) }, CLEAR_CLIPBOARD_MILLIS)
        }
        clipboard.setPrimaryClip(clip)
    }

    private fun clearIfUnchanged(clipboard: ClipboardManager, text: String) {
        try {
            val current = clipboard.primaryClip?.getItemAt(0)?.text?.toString()
            // can't verify the content in the background (null): don't clear something copied by the user
            if (current == text) {
                if (Build.VERSION.SDK_INT >= 28)
                    clipboard.clearPrimaryClip()
                else
                    clipboard.setPrimaryClip(ClipData.newPlainText("", ""))
            }
        } catch (e: SecurityException) {
            // the app may not read the clipboard in the background
        }
    }

    companion object {
        private const val CLEAR_CLIPBOARD_MILLIS = 60_000L
    }
}
