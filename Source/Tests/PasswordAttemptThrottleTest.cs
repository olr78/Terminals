using System;
using System.IO;
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

        [TestMethod]
        public void PersistedFailures_NewInstance_KeepsLock()
        {
            string file = CreateStateFileName();
            try
            {
                PasswordAttemptThrottle first = this.CreateThrottle(file);
                for (int index = 0; index < PasswordAttemptThrottle.FREE_ATTEMPTS + 1; index++)
                    first.RegisterFailure();

                // like application restart
                PasswordAttemptThrottle second = this.CreateThrottle(file);
                Assert.AreEqual(PasswordAttemptThrottle.FREE_ATTEMPTS + 1, second.Failures);
                Assert.AreEqual(TimeSpan.FromSeconds(4), second.RemainingDelay);

                second.RegisterFailure();
                Assert.AreEqual(TimeSpan.FromSeconds(8), first.RemainingDelay, "Other instance has to see the shared state.");
            }
            finally
            {
                File.Delete(file);
            }
        }

        [TestMethod]
        public void PersistedFailures_Success_DeletesStateFile()
        {
            string file = CreateStateFileName();
            PasswordAttemptThrottle throttle = this.CreateThrottle(file);
            throttle.RegisterFailure();
            Assert.IsTrue(File.Exists(file));

            throttle.RegisterSuccess();

            Assert.IsFalse(File.Exists(file));
            Assert.AreEqual(0, this.CreateThrottle(file).Failures);
        }

        [TestMethod]
        public void CorruptedStateFile_RemainingDelay_IsZero()
        {
            string file = CreateStateFileName();
            try
            {
                File.WriteAllText(file, "garbage");
                Assert.AreEqual(TimeSpan.Zero, this.CreateThrottle(file).RemainingDelay);

                // clock moved back: lock can't last longer than the maximum delay
                File.WriteAllText(file, "10 " + this.now.AddDays(1).Ticks);
                Assert.AreEqual(TimeSpan.FromSeconds(PasswordAttemptThrottle.MAX_DELAY_SECONDS), this.CreateThrottle(file).RemainingDelay);
            }
            finally
            {
                File.Delete(file);
            }
        }

        private static string CreateStateFileName()
        {
            return Path.Combine(Path.GetTempPath(), "TerminalsPasswordAttempts_" + Guid.NewGuid().ToString("N") + ".txt");
        }

        private PasswordAttemptThrottle CreateThrottle(string stateFile)
        {
            return new PasswordAttemptThrottle(() => this.now, () => stateFile);
        }

        private PasswordAttemptThrottle CreateThrottle()
        {
            return new PasswordAttemptThrottle(() => this.now);
        }
    }
}
