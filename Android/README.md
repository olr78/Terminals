# Terminals for Android (companion)

Android companion of the desktop Terminals: browse and search the connections, read the notes,
copy user names and passwords, and open the connection in an installed client app.
The app has no network permission and doesn't connect anywhere by itself.

| Protocol | Opened by |
| --- | --- |
| RDP | Windows App / Microsoft Remote Desktop (`rdp://` URI), other clients via `.rdp` file |
| SSH, Telnet | any app handling `ssh://` / `telnet://` (JuiceSSH, Termius, ...) |
| VNC | any app handling `vnc://` (RealVNC Viewer, ...) |
| HTTP, HTTPS | browser |

The password isn't passed to the clients; the app copies it to the clipboard (marked as sensitive,
cleared after a minute) before opening the connection.

## Data

The source is the **encrypted export** of the desktop Terminals (File → Export connections →
"Encrypt exported file with a password", optionally "Include Passwords"). The app stores only the
encrypted file in its private storage (excluded from backups) and decrypts it in memory after the
export password is entered. The app locks after 5 minutes in the background; failed password attempts
are slowed down the same way as in the desktop app (3 free attempts, then 2 ... 60 s).

Supported export formats: stored key of Terminals 4.2.12+ (PBKDF2-HMAC-SHA256, iterations stored
in the key) and the legacy key of older versions (PBKDF2-HMAC-SHA1, 2121 iterations).
See `core/ExportCrypto.kt` and `Source/Terminals/Integration/EncryptedExportFile.cs`.

## Build

Requirements: JDK 17, Android SDK (platform 35, build-tools 35.0.0) in `local.properties`
(`sdk.dir=...`).

```
gradlew testDebugUnitTest      # unit tests (crypto, parser, throttle)
gradlew assembleDebug          # app/build/outputs/apk/debug/app-debug.apk
gradlew assembleRelease        # signed, if keystore.properties exists
```

`keystore.properties` (not committed): `storeFile`, `storePassword`, `keyAlias`, `keyPassword`.

The test fixtures in `app/src/test/resources` were created by the desktop code:
`export-v4212.xml` by `EncryptedExportFile.WriteEncrypted` of Terminals 4.2.12 and `export-legacy.xml`
with the legacy key derivation; the password is `Пароль ü 1`, the plain content is `plain-favorites.xml`.
