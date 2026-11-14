using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Security;

namespace Tests
{
    [TestClass]
    public class PasswordAttemptThrottleTest
    {
        private DateTime now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void FirstFailures_RemainingDelay_IsZero()
        {
            PasswordAttemptThrottle throttle = this.CreateThrottle();
            for (int index = 1; index < PasswordAttemptThrottle.FREE_ATTEMPTS; index++)
                throttle.RegisterFailure();

            Assert.AreEqual(TimeSpan.Zero, throttle.RemainingDelay);
        }

        [TestMethod]
        public void RepeatedFailures_DelayAfter_GrowsUpToMaximum()
        {
            int free = PasswordAttemptThrottle.FREE_ATTEMPTS;
            Assert.AreEqual(TimeSpan.Zero, PasswordAttemptThrottle.DelayAfter(free - 1));
            Assert.AreEqual(TimeSpan.FromSeconds(2), PasswordAttemptThrottle.DelayAfter(free));
            Assert.AreEqual(TimeSpan.FromSeconds(4), PasswordAttemptThrottle.DelayAfter(free + 1));
            Assert.AreEqual(TimeSpan.FromSeconds(32), PasswordAttemptThrottle.DelayAfter(free + 4));
            Assert.AreEqual(TimeSpan.FromSeconds(PasswordAttemptThrottle.MAX_DELAY_SECONDS), PasswordAttemptThrottle.DelayAfter(free + 5));
            Assert.AreEqual(TimeSpan.FromSeconds(PasswordAttemptThrottle.MAX_DELAY_SECONDS), PasswordAttemptThrottle.DelayAfter(int.MaxValue));
        }

        [TestMethod]
        public void TooManyFailures_RemainingDelay_ExpiresInTime()
        {
            PasswordAttemptThrottle throttle = this.CreateThrottle();
            for (int index = 0; index < PasswordAttemptThrottle.FREE_ATTEMPTS; index++)
                throttle.RegisterFailure();

            Assert.AreEqual(TimeSpan.FromSeconds(2), throttle.RemainingDelay);
            this.now = this.now.AddSeconds(1.5);
            Assert.AreEqual(TimeSpan.FromSeconds(0.5), throttle.RemainingDelay);
            this.now = this.now.AddSeconds(1);
            Assert.AreEqual(TimeSpan.Zero, throttle.RemainingDelay);
        }

        [TestMethod]
        public void Success_Register_ResetsFailures()
        {
            PasswordAttemptThrottle throttle = this.CreateThrottle();
            for (int index = 0; index < PasswordAttemptThrottle.FREE_ATTEMPTS + 2; index++)
                Assert.IsFalse(throttle.Register(false));

            Assert.IsTrue(throttle.Register(true));

            Assert.AreEqual(0, throttle.Failures);
            Assert.AreEqual(TimeSpan.Zero, throttle.RemainingDelay);
        }

        private PasswordAttemptThrottle CreateThrottle()
        {
            return new PasswordAttemptThrottle(() => this.now);
        }
    }
}
