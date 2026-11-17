package net.terminals.companion.core

/**
 * Slows down guessing of the export password, same policy as the desktop PasswordAttemptThrottle:
 * after 3 free failed attempts the next attempt is allowed after 2, 4, 8 ... up to 60 seconds.
 * The state is persisted by [Store], so restarting the app doesn't reset it.
 */
class AttemptThrottle(private val store: Store, private val now: () -> Long = System::currentTimeMillis) {
    interface Store {
        var failures: Int
        var lockedUntilMillis: Long
    }

    val remainingMillis: Long
        get() {
            val remaining = store.lockedUntilMillis - now()
            // clock moved back: the lock can't be longer than the maximum delay
            return remaining.coerceIn(0, MAX_DELAY_SECONDS * 1000L)
        }

    fun registerFailure() {
        val failures = if (store.failures == Int.MAX_VALUE) store.failures else store.failures + 1
        store.failures = failures
        val delay = delayAfter(failures)
        if (delay > 0)
            store.lockedUntilMillis = now() + delay * 1000L
    }

    fun registerSuccess() {
        store.failures = 0
        store.lockedUntilMillis = 0
    }

    companion object {
        const val FREE_ATTEMPTS = 3
        const val MAX_DELAY_SECONDS = 60

        fun delayAfter(failures: Int): Int {
            if (failures < FREE_ATTEMPTS)
                return 0
            val exponent = minOf(failures - FREE_ATTEMPTS + 1, 6)
            return minOf(1 shl exponent, MAX_DELAY_SECONDS)
        }
    }
}
