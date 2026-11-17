package net.terminals.companion.core

import java.io.StringReader
import java.security.MessageDigest
import java.util.Base64
import javax.crypto.Cipher
import javax.crypto.Mac
import javax.crypto.SecretKeyFactory
import javax.crypto.spec.IvParameterSpec
import javax.crypto.spec.PBEKeySpec
import javax.crypto.spec.SecretKeySpec
import javax.xml.parsers.DocumentBuilderFactory
import org.xml.sax.InputSource

class WrongPasswordException : Exception("Wrong password")

class InvalidExportException(message: String, cause: Throwable? = null) : Exception(message, cause)

/**
 * Decrypts the password protected export of the desktop Terminals (EncryptedExportFile.cs):
 *
 * <encryptedFavorites><storedKey>..</storedKey><payload>..</payload></encryptedFavorites>
 *
 * storedKey (Base64) in the current format (53 B, Terminals 4.2.12+): master key salt (16 B),
 * validation hash HMAC-SHA256(key, label) (32 B), format id 2 (1 B), iterations (Int32 LE),
 * key = PBKDF2-HMAC-SHA256(password, salt, iterations, 32 B).
 * Legacy format (64 B): master key salt (16 B), validation salt (16 B), validation key (32 B),
 * all derived by PBKDF2-HMAC-SHA1 with 2121 iterations.
 *
 * payload (Base64): IV (16 B) + AES-256-CBC/PKCS7 of the export XML encoded as UTF-16LE.
 * Passwords are encoded as UTF-8 for PBKDF2, same as .NET Rfc2898DeriveBytes and bcrypt.
 */
object ExportCrypto {
    private const val IV_LENGTH = 16
    private const val KEY_LENGTH = 32
    private const val STORED_KEY_LENGTH = IV_LENGTH + KEY_LENGTH + 1 + 4
    private const val LEGACY_STORED_KEY_LENGTH = 2 * IV_LENGTH + KEY_LENGTH
    private const val FORMAT_PBKDF2_SHA256 = 2
    private const val LEGACY_ITERATIONS = 2121
    private const val MAX_ITERATIONS = 100_000_000
    private val validationLabel = "Terminals master password validation".toByteArray(Charsets.US_ASCII)

    data class EncryptedExport(val storedKey: ByteArray, val payload: ByteArray)

    /** Returns true, if the document looks like the encrypted export, without decrypting it. */
    fun isEncryptedExport(xml: String): Boolean =
        try {
            parse(xml)
            true
        } catch (e: InvalidExportException) {
            false
        }

    fun parse(xml: String): EncryptedExport {
        try {
            val factory = DocumentBuilderFactory.newInstance()
            factory.isExpandEntityReferences = false
            // the desktop app saves the file as UTF-8 with BOM
            val document = factory.newDocumentBuilder().parse(InputSource(StringReader(xml.removePrefix("﻿"))))
            val root = document.documentElement
            if (root.nodeName != "encryptedFavorites")
                throw InvalidExportException("Not an encrypted Terminals export")

            fun child(name: String): String {
                val nodes = root.getElementsByTagName(name)
                if (nodes.length == 0)
                    throw InvalidExportException("Missing element $name")
                return nodes.item(0).textContent.trim()
            }

            val decoder = Base64.getMimeDecoder()
            return EncryptedExport(decoder.decode(child("storedKey")), decoder.decode(child("payload")))
        } catch (e: InvalidExportException) {
            throw e
        } catch (e: Exception) {
            throw InvalidExportException("Unable to read the export file", e)
        }
    }

    /** Decrypts the export and returns the plain favorites XML. */
    fun decrypt(export: EncryptedExport, password: CharArray): String {
        val key = deriveKey(export.storedKey, password)
        try {
            val payload = export.payload
            if (payload.size <= IV_LENGTH)
                throw InvalidExportException("Empty payload")

            val cipher = Cipher.getInstance("AES/CBC/PKCS5Padding")
            cipher.init(Cipher.DECRYPT_MODE, SecretKeySpec(key, "AES"), IvParameterSpec(payload, 0, IV_LENGTH))
            val plain = cipher.doFinal(payload, IV_LENGTH, payload.size - IV_LENGTH)
            try {
                return String(plain, Charsets.UTF_16LE).removePrefix("﻿")
            } finally {
                plain.fill(0)
            }
        } catch (e: InvalidExportException) {
            throw e
        } catch (e: Exception) {
            throw InvalidExportException("Unable to decrypt the export", e)
        } finally {
            key.fill(0)
        }
    }

    /** Validates the password against the stored key and returns the 32 B AES key. */
    internal fun deriveKey(storedKey: ByteArray, password: CharArray): ByteArray {
        if (password.isEmpty())
            throw WrongPasswordException()

        if (storedKey.size == STORED_KEY_LENGTH && storedKey[IV_LENGTH + KEY_LENGTH].toInt() == FORMAT_PBKDF2_SHA256) {
            val index = IV_LENGTH + KEY_LENGTH + 1
            val iterations = (storedKey[index].toInt() and 0xFF) or
                    ((storedKey[index + 1].toInt() and 0xFF) shl 8) or
                    ((storedKey[index + 2].toInt() and 0xFF) shl 16) or
                    ((storedKey[index + 3].toInt() and 0xFF) shl 24)
            if (iterations <= 0 || iterations > MAX_ITERATIONS)
                throw InvalidExportException("Invalid iteration count")

            val key = pbkdf2("PBKDF2WithHmacSHA256", password, storedKey.copyOfRange(0, IV_LENGTH), iterations)
            val expected = storedKey.copyOfRange(IV_LENGTH, IV_LENGTH + KEY_LENGTH)
            if (!MessageDigest.isEqual(validationHash(key), expected)) {
                key.fill(0)
                throw WrongPasswordException()
            }
            return key
        }

        if (storedKey.size == LEGACY_STORED_KEY_LENGTH) {
            val validationSalt = storedKey.copyOfRange(IV_LENGTH, 2 * IV_LENGTH)
            val validationKey = storedKey.copyOfRange(2 * IV_LENGTH, storedKey.size)
            val check = pbkdf2("PBKDF2WithHmacSHA1", password, validationSalt, LEGACY_ITERATIONS)
            if (!MessageDigest.isEqual(check, validationKey))
                throw WrongPasswordException()

            return pbkdf2("PBKDF2WithHmacSHA1", password, storedKey.copyOfRange(0, IV_LENGTH), LEGACY_ITERATIONS)
        }

        throw InvalidExportException("Unknown stored key format (${storedKey.size} B)")
    }

    internal fun pbkdf2(algorithm: String, password: CharArray, salt: ByteArray, iterations: Int): ByteArray {
        val spec = PBEKeySpec(password, salt, iterations, KEY_LENGTH * 8)
        try {
            return SecretKeyFactory.getInstance(algorithm).generateSecret(spec).encoded
        } finally {
            spec.clearPassword()
        }
    }

    private fun validationHash(key: ByteArray): ByteArray {
        val mac = Mac.getInstance("HmacSHA256")
        mac.init(SecretKeySpec(key, "HmacSHA256"))
        return mac.doFinal(validationLabel)
    }
}
