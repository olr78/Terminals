package net.terminals.companion.ui

import android.app.Application
import android.net.Uri
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import net.terminals.companion.R
import net.terminals.companion.core.Favorite
import net.terminals.companion.core.InvalidExportException
import net.terminals.companion.core.WrongPasswordException
import net.terminals.companion.data.Vault

sealed class VaultState {
    object Empty : VaultState()
    data class Locked(val errorId: Int? = null, val waitSeconds: Int = 0, val working: Boolean = false) : VaultState()
    data class Unlocked(val favorites: List<Favorite>) : VaultState()
}

class VaultViewModel(application: Application) : AndroidViewModel(application) {
    private val vault = Vault(application)

    private val _state = MutableStateFlow(initialState())
    val state: StateFlow<VaultState> = _state

    private val _message = MutableStateFlow<Int?>(null)
    val message: StateFlow<Int?> = _message

    private var backgroundSinceMillis = 0L

    private fun initialState(): VaultState = if (vault.hasExport) lockedState() else VaultState.Empty

    private fun lockedState(errorId: Int? = null): VaultState.Locked {
        val wait = ((vault.throttle.remainingMillis + 999) / 1000).toInt()
        if (wait > 0)
            startCountdown()
        return VaultState.Locked(errorId, wait)
    }

    private var countdownRunning = false

    private fun startCountdown() {
        if (countdownRunning)
            return
        countdownRunning = true
        viewModelScope.launch {
            try {
                while (true) {
                    delay(250)
                    val current = _state.value as? VaultState.Locked ?: break
                    val wait = ((vault.throttle.remainingMillis + 999) / 1000).toInt()
                    _state.value = current.copy(waitSeconds = wait)
                    if (wait == 0)
                        break
                }
            } finally {
                countdownRunning = false
            }
        }
    }

    fun unlock(password: CharArray) {
        val current = _state.value as? VaultState.Locked ?: return
        if (current.working || vault.throttle.remainingMillis > 0) {
            password.fill(' ')
            return
        }

        _state.value = current.copy(working = true, errorId = null)
        viewModelScope.launch {
            val result = withContext(Dispatchers.Default) {
                try {
                    vault.unlock(password)
                } catch (e: WrongPasswordException) {
                    null
                } catch (e: InvalidExportException) {
                    e
                } finally {
                    password.fill(' ')
                }
            }

            @Suppress("UNCHECKED_CAST")
            _state.value = when (result) {
                null -> {
                    vault.throttle.registerFailure()
                    lockedState(R.string.wrong_password)
                }
                is InvalidExportException -> lockedState(R.string.invalid_export)
                else -> {
                    vault.throttle.registerSuccess()
                    VaultState.Unlocked(result as List<Favorite>)
                }
            }
        }
    }

    fun import(uri: Uri) {
        viewModelScope.launch {
            val ok = withContext(Dispatchers.IO) {
                try {
                    vault.import(uri)
                    true
                } catch (e: Exception) {
                    false
                }
            }
            if (ok) {
                _state.value = lockedState()
                _message.value = R.string.import_done
            } else {
                _message.value = R.string.invalid_export
            }
        }
    }

    fun lock() {
        if (vault.hasExport)
            _state.value = lockedState()
    }

    fun deleteExport() {
        vault.delete()
        _state.value = VaultState.Empty
    }

    fun messageShown() {
        _message.value = null
    }

    fun onBackground() {
        backgroundSinceMillis = System.currentTimeMillis()
    }

    /** Locks after the app was in the background longer than the timeout (e.g. not just switched to the RDP client). */
    fun onForeground() {
        val since = backgroundSinceMillis
        backgroundSinceMillis = 0
        if (since > 0 && System.currentTimeMillis() - since > AUTO_LOCK_MILLIS && _state.value is VaultState.Unlocked)
            lock()
    }

    companion object {
        const val AUTO_LOCK_MILLIS = 5 * 60_000L
    }
}
