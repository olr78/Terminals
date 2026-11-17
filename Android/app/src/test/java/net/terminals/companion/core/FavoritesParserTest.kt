package net.terminals.companion.core

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class FavoritesParserTest {
    private val favorites = FavoritesParser.parse(
        javaClass.classLoader!!.getResource("plain-favorites.xml")!!.readText(Charsets.UTF_8))

    @Test
    fun desktopExport_Parse_ReadsAllFields() {
        assertEquals(listOf("linux box", "Portal", "vnc", "Сервер 1"), favorites.map { it.name })

        val rdp = favorites.single { it.protocol == Protocols.RDP }
        assertEquals("srv1.example.local", rdp.serverName)
        assertEquals(3389, rdp.port)
        assertEquals(listOf("Office", "Production"), rdp.groups)
        assertEquals("CORP\\admin", rdp.fullUserName)
        assertEquals("p@ss'word\"", rdp.password)
        assertEquals("line1\nline2 <&>", rdp.notes.replace("\r\n", "\n"))
        assertEquals("srv1.example.local", rdp.address)

        val ssh = favorites.single { it.protocol == Protocols.SSH }
        assertEquals("10.0.0.5:2222", ssh.address)
        assertEquals("root", ssh.fullUserName)

        val vnc = favorites.single { it.protocol == Protocols.VNC }
        assertEquals("", vnc.userName)
        assertEquals(emptyList<String>(), vnc.groups)

        assertEquals("https://portal.example.local/login", favorites.single { it.protocol == Protocols.HTTPS }.url)
    }

    @Test
    fun words_Matches_SearchesAllTextFields() {
        val rdp = favorites.single { it.protocol == Protocols.RDP }
        assertTrue(rdp.matches("сервер prod"))
        assertTrue(rdp.matches("line2"))
        assertTrue(rdp.matches(""))
        assertFalse(rdp.matches("сервер linux"))
        assertTrue(favorites.single { it.protocol == Protocols.SSH }.matches("2222"))
        assertFalse(rdp.matches("2222"))
    }
}
