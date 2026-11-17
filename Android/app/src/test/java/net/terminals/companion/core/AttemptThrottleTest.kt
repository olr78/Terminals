package net.terminals.companion.core

import org.junit.Assert.assertEquals
import org.junit.Test

class AttemptThrottleTest {
    private var now = 1_000_000L

    private class MemoryStore : AttemptThrottle.Store {
        override var failures = 0
        override var lockedUntilMillis = 0L
    }

    private val store = MemoryStore()
    private val throttle = AttemptThrottle(store) { now }

    @Test
    fun delays_GrowUpToMaximum() {
        assertEquals(listOf(0, 0, 2, 4, 8, 16, 32, 60, 60), (1..9).map { AttemptThrottle.delayAfter(it) })
        assertEquals(60, AttemptThrottle.delayAfter(Int.MAX_VALUE))
    }

    @Test
    fun failures_Lock_UntilDelayExpires() {
        repeat(2) { throttle.registerFailure() }
        assertEquals(0, throttle.remainingMillis)

        throttle.registerFailure()
        assertEquals(2000, throttle.remainingMillis)
        now += 1500
        assertEquals(500, throttle.remainingMillis)
        now += 600
        assertEquals(0, throttle.remainingMillis)
    }

    @Test
    fun persistedState_NewInstance_KeepsLock() {
        repeat(4) { throttle.registerFailure() }
        assertEquals(4000, AttemptThrottle(store) { now }.remainingMillis)
    }

    @Test
    fun success_ResetsAndClockBack_IsCapped() {
        repeat(5) { throttle.registerFailure() }
        throttle.registerSuccess()
        assertEquals(0, store.failures)
        assertEquals(0, throttle.remainingMillis)

        store.lockedUntilMillis = now + 86_400_000
        assertEquals(60_000, throttle.remainingMillis)
    }
}
