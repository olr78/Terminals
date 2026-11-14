using System;
using System.Globalization;
using System.IO;
using Terminals.Configuration;

namespace Terminals.Security
{
    /// <summary>
    /// Slows down guessing of passwords entered in the password prompt.
    /// After a few free failed attempts, each next attempt has to wait exponentially longer
    /// (2, 4, 8 ... seconds up to the maximum). Successful authentication resets the counter.
    /// If the state file is defined, the counter survives application restarts and is shared
    /// by all running instances, because the state is read from the file before each use.
    /// </summary>
    internal class PasswordAttemptThrottle
    {
        internal const int FREE_ATTEMPTS = 3;

        internal const int MAX_DELAY_SECONDS = 60;

        /// <summary>
        /// Shared by all prompts validating the application master password, persisted in the data directory.
        /// </summary>
        internal static readonly PasswordAttemptThrottle MasterPassword =
            new PasswordAttemptThrottle(() => DateTime.UtcNow, () => FileLocations.PasswordAttempts);

        /// <summary>
        /// Used by the import of password protected export files.
        /// </summary>
        internal static readonly PasswordAttemptThrottle ExportFilePassword = new PasswordAttemptThrottle();

        private readonly object syncRoot = new object();

        private readonly Func<DateTime> now;

        private readonly Func<string> stateFile;

        /// <summary>
        /// Set after the state file couldn't be written, to keep counting in memory.
        /// </summary>
        private bool persistenceFailed;

        private int failures;

        private DateTime lockedUntil = DateTime.MinValue;

        internal PasswordAttemptThrottle()
            : this(() => DateTime.UtcNow)
        {
        }

        internal PasswordAttemptThrottle(Func<DateTime> now)
            : this(now, null)
        {
        }

        /// <param name="now">Source of current UTC time</param>
        /// <param name="stateFile">Optional source of full path to the file, where to persist the state.</param>
        internal PasswordAttemptThrottle(Func<DateTime> now, Func<string> stateFile)
        {
            this.now = now;
            this.stateFile = stateFile;
        }

        internal int Failures
        {
            get
            {
                lock (this.syncRoot)
                {
                    this.Load();
                    return this.failures;
                }
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
                    this.Load();
                    TimeSpan remaining = this.lockedUntil - this.now();
                    if (remaining <= TimeSpan.Zero)
                        return TimeSpan.Zero;

                    // system clock moved back or the state file was modified
                    TimeSpan maximum = TimeSpan.FromSeconds(MAX_DELAY_SECONDS);
                    return remaining > maximum ? maximum : remaining;
                }
            }
        }

        internal void RegisterFailure()
        {
            int currentFailures;
            lock (this.syncRoot)
            {
                this.Load();
                if (this.failures < int.MaxValue)
                    this.failures++;

                TimeSpan delay = DelayAfter(this.failures);
                if (delay > TimeSpan.Zero)
                    this.lockedUntil = this.now() + delay;

                this.Save();
                currentFailures = this.failures;
            }

            Logging.Warn(string.Format("Invalid password entered ({0} failed attempts in a row).", currentFailures));
        }

        internal void RegisterSuccess()
        {
            lock (this.syncRoot)
            {
                this.Load();
                bool changed = this.failures != 0;
                this.failures = 0;
                this.lockedUntil = DateTime.MinValue;
                if (changed)
                    this.Save();
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

        /// <summary>
        /// File format: one line "failures lockedUntilUtcTicks". Missing or unreadable file keeps the state in memory.
        /// </summary>
        private void Load()
        {
            if (this.stateFile == null || this.persistenceFailed)
                return;

            try
            {
                string fileName = this.stateFile();
                if (!File.Exists(fileName))
                {
                    this.failures = 0;
                    this.lockedUntil = DateTime.MinValue;
                    return;
                }

                string[] parts = File.ReadAllText(fileName).Trim().Split(' ');
                int loadedFailures;
                long ticks;
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out loadedFailures) &&
                    long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out ticks) &&
                    ticks <= DateTime.MaxValue.Ticks)
                {
                    this.failures = loadedFailures;
                    this.lockedUntil = new DateTime(ticks, DateTimeKind.Utc);
                }
            }
            catch (Exception exception)
            {
                Logging.Warn("Unable to read failed password attempts: " + exception.Message);
            }
        }

        private void Save()
        {
            if (this.stateFile == null || this.persistenceFailed)
                return;

            try
            {
                string fileName = this.stateFile();
                if (this.failures == 0)
                {
                    File.Delete(fileName);
                    return;
                }

                string content = this.failures.ToString(CultureInfo.InvariantCulture) + " " +
                                 this.lockedUntil.Ticks.ToString(CultureInfo.InvariantCulture);
                File.WriteAllText(fileName, content);
            }
            catch (Exception exception)
            {
                this.persistenceFailed = true;
                Logging.Warn("Unable to save failed password attempts: " + exception.Message);
            }
        }
    }
}
