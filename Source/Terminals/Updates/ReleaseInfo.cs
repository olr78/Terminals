using System;

namespace Terminals.Updates
{
    /// <summary>
    /// Description of new release
    /// </summary>
    internal class ReleaseInfo
    {
        /// <summary>
        /// Gets flag informing, that new release is available
        /// </summary>
        internal bool NewAvailable { get; private set; }
        internal DateTime Published { get; private set; }
        internal string Version { get; private set; }

        /// <summary>
        /// Gets the release obtained from GitHub. Null, if new release isn't available.
        /// </summary>
        internal Release Release { get; private set; }

        /// <summary>
        /// Gets flag informing, that the check itself failed (e.g. network is not available).
        /// </summary>
        internal bool CheckFailed { get; private set; }

        private static readonly ReleaseInfo notAvailable = new ReleaseInfo(DateTime.MinValue, "Not available")
            {
                NewAvailable = false
            };

        private static readonly ReleaseInfo failed = new ReleaseInfo(DateTime.MinValue, "Not available")
            {
                NewAvailable = false,
                CheckFailed = true
            };

        /// <summary>
        /// Gets result of update check in a case new release is not available.
        /// </summary>
        internal static ReleaseInfo NotAvailable { get { return notAvailable; }}

        /// <summary>
        /// Gets result of update check in a case the check wasn't able to finish.
        /// </summary>
        internal static ReleaseInfo Failed { get { return failed; } }

        internal ReleaseInfo(DateTime published, string version)
        {
            this.NewAvailable = true;
            this.Published = published;
            this.Version = version;
        }

        internal ReleaseInfo(Release release)
            : this(release.Published, release.Version.ToString())
        {
            this.Release = release;
        }

        public override string ToString()
        {
            return string.Format("ReleaseInfo:{0},Published={1},NewAvailable={2}",
                                 this.Version, this.Published, this.NewAvailable);
        }
    }
}
