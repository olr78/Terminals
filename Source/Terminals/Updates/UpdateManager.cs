using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Terminals.Properties;

namespace Terminals.Updates
{
    internal class UpdateManager
    {
        private const string USER_AGENT = "Terminals-Updater";

        private readonly Func<string> readReleases;

        public UpdateManager() : this(DownloadReleases)
        {
        }

        internal UpdateManager(Func<string> readReleases)
        {
            this.readReleases = readReleases;
        }

        /// <summary>
        /// GitHub accepts TLS 1.2 only. Enabled for whole application, because the downloads run asynchronously.
        /// </summary>
        internal static void EnableTls12()
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // 3072 = System.Net.SecurityProtocolType.Tls12
        }

        internal static WebClient CreateWebClient()
        {
            EnableTls12();
            var client = new WebClient();
            client.Headers.Add("User-Agent", USER_AGENT);
            return client;
        }

        private static string DownloadReleases()
        {
            using (WebClient client = CreateWebClient())
            {
                client.Headers.Add("Accept", "application/vnd.github+json");
                client.Encoding = System.Text.Encoding.UTF8;
                return client.DownloadString(Settings.Default.ReleasesUrl);
            }
        }

        /// <summary>
        /// Check for available application updates.
        /// </summary>
        /// <param name="forceCheck">If true, the check is performed even it was already done today.</param>
        internal Task<ReleaseInfo> CheckForUpdates(bool forceCheck)
        {
            return Task<ReleaseInfo>.Factory.StartNew(force => this.CheckForPublishedRelease(Program.Info.Version, (bool)force), forceCheck);
        }

        internal ReleaseInfo CheckForPublishedRelease(Version currentVersion)
        {
            return this.CheckForPublishedRelease(currentVersion, false);
        }

        internal ReleaseInfo CheckForPublishedRelease(Version currentVersion, bool forceCheck)
        {
            try
            {
                return this.TryCheckForPublishedRelease(currentVersion, forceCheck);
            }
            catch (Exception exception)
            {
                Logging.Error("Failed during Check for release.", exception);
                return ReleaseInfo.Failed;
            }
        }

        /// <summary>
        /// Check GitHub releases to see if we have a new release available.
        /// Returns not null info about obtained current release.
        /// ReleaseInfo.NotAvailable in a case, new version was not checked or current version is the latest.
        /// </summary>
        private ReleaseInfo TryCheckForPublishedRelease(Version currentVersion, bool forceCheck)
        {
            var checksFile = new UpdateChecksFile();
            if (!forceCheck && !checksFile.ShouldCheckForUpdate)
                return ReleaseInfo.NotAvailable;

            ReleaseInfo downLoaded = this.DownLoadLatestReleaseInfo(currentVersion);
            checksFile.WriteLastCheck();
            return downLoaded;
        }

        private ReleaseInfo DownLoadLatestReleaseInfo(Version currentVersion)
        {
            string downloaded = this.readReleases();
            Release[] feed = JsonConvert.DeserializeObject<Release[]>(downloaded);

            if (feed != null)
            {
                Release newestRelease = SelectNewestRelease(feed, currentVersion);
                if (newestRelease != null)
                    return new ReleaseInfo(newestRelease);
            }

            return ReleaseInfo.NotAvailable;
        }

        private static Release SelectNewestRelease(Release[] feed, Version currentVersion)
        {
            Version current = Release.Normalize(currentVersion);
            return feed.Where(item => !item.Draft && !item.Prerelease && item.Version != null && item.Version > current)
                       .OrderByDescending(selected => selected.Version)
                       .FirstOrDefault();
        }
    }
}
