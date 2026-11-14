using System;

namespace Terminals.Security
{
    /// <summary>
    /// Slows down guessing of passwords entered in the password prompt.
    /// After a few free failed attempts, each next attempt has to wait exponentially longer
    /// (2, 4, 8 ... seconds up to the maximum). Successful authentication resets the counter.
    /// </summary>
    internal class PasswordAttemptThrottle
    {
        internal const int FREE_ATTEMPTS = 3;

        internal const int MAX_DELAY_SECONDS = 60;

        /// <summary>
        /// Shared by all prompts validating the application master password.
        /// </summary>
        internal static readonly PasswordAttemptThrottle MasterPassword = new PasswordAttemptThrottle();

        /// <summary>
        /// Used by the import of password protected export files.
        /// </summary>
        internal static readonly PasswordAttemptThrottle ExportFilePassword = new PasswordAttemptThrottle();

        private readonly object syncRoot = new object();

        private readonly Func<DateTime> now;

        private int failures;

        private DateTime lockedUntil = DateTime.MinValue;

        internal PasswordAttemptThrottle()
            : this(() => DateTime.UtcNow)
        {
        }

        internal PasswordAttemptThrottle(Func<DateTime> now)
        {
            this.now = now;
        }

        internal int Failures
        {
            get
            {
                lock (this.syncRoot)
                    return this.failures;
            }
        }

        /// <summary>
        /// Gets time, which user has to wait before the next attempt is allowed. Zero, if not locked.
        /// </summary>
        internal TimeSpan RemainingDelay
        {
            get
            {
                lock (this.syncRoot)
                {
                    TimeSpan remaining = this.lockedUntil - this.now();
                    return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
                }
            }
        }

        internal void RegisterFailure()
        {
            lock (this.syncRoot)
            {
                this.failures++;
                TimeSpan delay = DelayAfter(this.failures);
                if (delay > TimeSpan.Zero)
                    this.lockedUntil = this.now() + delay;
            }

            Logging.Warn(string.Format("Invalid password entered ({0} failed attempts in a row).", this.Failures));
        }

        internal void RegisterSuccess()
        {
            lock (this.syncRoot)
            {
                this.failures = 0;
                this.lockedUntil = DateTime.MinValue;
            }
        }

        /// <summary>
        /// Registers result of the password validation and returns the same result.
        /// </summary>
        internal bool Register(bool isValid)
        {
            if (isValid)
                this.RegisterSuccess();
            else
                this.RegisterFailure();

            return isValid;
        }

        internal static TimeSpan DelayAfter(int failures)
        {
            if (failures < FREE_ATTEMPTS)
                return TimeSpan.Zero;

            int exponent = Math.Min(failures - FREE_ATTEMPTS + 1, 6);
            int seconds = Math.Min(1 << exponent, MAX_DELAY_SECONDS);
            return TimeSpan.FromSeconds(seconds);
        }
    }
}
