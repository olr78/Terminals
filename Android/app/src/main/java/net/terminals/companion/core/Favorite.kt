package net.terminals.companion.core

import java.io.StringReader
import javax.xml.parsers.DocumentBuilderFactory
import org.w3c.dom.Element
import org.xml.sax.InputSource

/** One connection from the desktop Terminals export (ExportTerminals.cs). */
data class Favorite(
    val name: String,
    val protocol: String,
    val serverName: String,
    val port: Int,
    val url: String,
    val notes: String,
    val groups: List<String>,
    val domain: String,
    val userName: String,
    val password: String,
) {
    /** User name including the domain, as used by the remote desktop clients. */
    val fullUserName: String
        get() = when {
            userName.isEmpty() -> ""
            domain.isEmpty() || userName.contains('\\') || userName.contains('@') -> userName
            else -> "$domain\\$userName"
        }

    val address: String
        get() = if (port > 0 && port != Protocols.defaultPort(protocol)) "$serverName:$port" else serverName

    fun matches(query: String): Boolean {
        if (query.isBlank())
            return true
        return query.trim().split(Regex("\\s+")).all { word ->
            name.contains(word, true) || serverName.contains(word, true) || notes.contains(word, true) ||
                    protocol.contains(word, true) || userName.contains(word, true) ||
                    groups.any { it.contains(word, true) }
        }
    }
}

object Protocols {
    const val RDP = "RDP"
    const val VNC = "VNC"
    const val SSH = "SSH"
    const val TELNET = "Telnet"
    const val HTTP = "HTTP"
    const val HTTPS = "HTTPS"

    fun defaultPort(protocol: String): Int = when (protocol.uppercase()) {
        "RDP" -> 3389
        "VNC" -> 5900
        "SSH" -> 22
        "TELNET" -> 23
        "HTTP" -> 80
        "HTTPS" -> 443
        else -> 0
    }
}

object FavoritesParser {
    fun parse(xml: String): List<Favorite> {
        try {
            val factory = DocumentBuilderFactory.newInstance()
            factory.isExpandEntityReferences = false
            val document = factory.newDocumentBuilder().parse(InputSource(StringReader(xml)))
            val nodes = document.documentElement.getElementsByTagName("favorite")
            return (0 until nodes.length)
                .map { toFavorite(nodes.item(it) as Element) }
                .sortedWith(compareBy(String.CASE_INSENSITIVE_ORDER) { it.name })
        } catch (e: Exception) {
            throw InvalidExportException("Unable to read the connections", e)
        }
    }

    private fun toFavorite(element: Element): Favorite {
        fun text(name: String): String {
            val nodes = element.getElementsByTagName(name)
            return if (nodes.length == 0) "" else nodes.item(0).textContent.trim()
        }

        val protocol = text("protocol").ifEmpty { Protocols.RDP }
        val serverName = text("serverName")
        return Favorite(
            name = text("name").ifEmpty { serverName },
            protocol = protocol,
            serverName = serverName,
            port = text("port").toIntOrNull() ?: Protocols.defaultPort(protocol),
            url = text("url"),
            notes = text("notes"),
            groups = text("tags").split(',').map { it.trim() }.filter { it.isNotEmpty() },
            domain = text("domainName"),
            userName = text("userName"),
            password = text("password"),
        )
    }
}
