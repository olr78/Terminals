using System;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Configuration;
using Terminals.Updates;
using Tests.FilePersisted;

namespace Tests
{
    /// <summary>
    /// Check for new release to be able parse release rss feeds, to be able to download and unpack the update package.
    /// Expected implementation of UpdateManager:
    /// - the update check file is written after all checks
    /// - release shouldn't be reported only, if there is a release with newer version, than current
    /// - new release should be reported once per day only
    /// </summary>
    [TestClass]
    public class UpdateManagerTest
    {
        private Version currentVersion = new Version(2, 0, 0);

        private readonly DateTime yesterDay = DateTime.UtcNow.Date.AddDays(-1);

        [TestInitialize]
        public void ConfigureTestLab()
        {
            FilePersistedTestLab.SetDefaultFileLocations();

            if (File.Exists(FileLocations.LastUpdateCheck))
                File.Delete(FileLocations.LastUpdateCheck);
        }

        /// <summary>
        /// in debug there is never a newer version, because it is checked by build date
        /// </summary>
        [TestMethod]
        public void CurrentBuild_CheckForCodeplexRelease_ReturnsNotAvailable()
        {
            this.currentVersion = new Version(4, 0, 0);
            ReleaseInfo checkResult = this.RunUpdateCheck();

            Assert.AreEqual(ReleaseInfo.NotAvailable, checkResult, "New release noticed");
            this.AssertLastUpdateCheck();
        }

        [TestMethod]
        public void OldestBuildDate_CheckForCodeplexRelease_ReturnsValidRelease()
        {
            this.currentVersion = new Version(1, 0, 0);
            ReleaseInfo checkResult = this.RunUpdateCheck();

            Assert.AreNotEqual(ReleaseInfo.NotAvailable, checkResult, "Didn't notice new release");
            this.AssertLastUpdateCheck();
        }

        [TestMethod]
        public void TodayCheckedDate_CheckForCodeplexRelease_DoesnotUpdateCheckDate()
        {
            var previousCheck = DateTime.UtcNow.Date;
            File.WriteAllText(FileLocations.LastUpdateCheck, previousCheck.ToString(CultureInfo.InvariantCulture));
            ReleaseInfo checkResult = this.RunUpdateCheck();

            Assert.AreEqual(ReleaseInfo.NotAvailable, checkResult, "New release noticed");
            DateTime lastNoticed = this.ParseLastUpdateDate();
            Assert.AreEqual(lastNoticed.Date, previousCheck.Date, "Last update check date wasn't saved");
        }

        [TestMethod]
        public void TodayCheckedDate_ForcedCheck_ReturnsValidRelease()
        {
            File.WriteAllText(FileLocations.LastUpdateCheck, DateTime.UtcNow.Date.ToString(CultureInfo.InvariantCulture));
            var updateManager = new UpdateManager(() => GitHubReleases);
            ReleaseInfo checkResult = updateManager.CheckForPublishedRelease(new Version(4, 0, 14, 1234), true);

            Assert.IsTrue(checkResult.NewAvailable, "Forced check didn't notice new release");
        }

        [TestMethod]
        public void GitHubTags_CheckForRelease_ReturnsNewestPublishedRelease()
        {
            var updateManager = new UpdateManager(() => GitHubReleases);
            ReleaseInfo checkResult = updateManager.CheckForPublishedRelease(new Version(4, 0, 14, 1234));

            Assert.AreEqual("4.0.15", checkResult.Version, "Draft or prerelease was selected or 'v' prefix wasn't parsed");
            Assert.AreEqual("abc123", checkResult.Release.FindAsset(".zip").Sha256);
            Assert.AreEqual("Terminals-v4.0.15.zip", checkResult.Release.FindAsset(".zip").Name);
        }

        [TestMethod]
        public void SameVersionWithBuildRevision_CheckForRelease_ReturnsNotAvailable()
        {
            var updateManager = new UpdateManager(() => GitHubReleases);
            ReleaseInfo checkResult = updateManager.CheckForPublishedRelease(new Version(4, 0, 15, 20000));

            Assert.AreEqual(ReleaseInfo.NotAvailable, checkResult, "Revision number of current build has to be ignored");
        }

        [TestMethod]
        public void InvalidDownload_CheckForRelease_ReturnsFailed()
        {
            var updateManager = new UpdateManager(() => { throw new System.Net.WebException("offline"); });
            ReleaseInfo checkResult = updateManager.CheckForPublishedRelease(this.currentVersion);

            Assert.IsTrue(checkResult.CheckFailed);
            Assert.IsFalse(checkResult.NewAvailable);
        }

        [TestMethod]
        public void UpstreamUrlFromOldConfig_ResolveReleasesUrl_ReturnsForkUrl()
        {
            string resolved = UpdateManager.ResolveReleasesUrl("https://api.github.com/repos/Terminals-Origin/Terminals/releases");
            Assert.AreEqual(UpdateManager.DEFAULT_RELEASES_URL, resolved);
        }

        [TestMethod]
        public void EmptyUrl_ResolveReleasesUrl_ReturnsForkUrl()
        {
            Assert.AreEqual(UpdateManager.DEFAULT_RELEASES_URL, UpdateManager.ResolveReleasesUrl(string.Empty));
        }

        [TestMethod]
        public void CustomUrl_ResolveReleasesUrl_KeepsConfiguredUrl()
        {
            const string custom = "https://api.github.com/repos/someone/Terminals/releases";
            Assert.AreEqual(custom, UpdateManager.ResolveReleasesUrl(custom));
        }

        private const string GitHubReleases = @"
[
{ ""tag_name"": ""v4.0.16"", ""draft"": true, ""prerelease"": false, ""published_at"": ""2026-09-26T10:00:00Z"" },
{ ""tag_name"": ""v4.0.17-beta"", ""draft"": false, ""prerelease"": true, ""published_at"": ""2026-09-26T10:00:00Z"" },
{ ""tag_name"": ""v4.0.15"", ""draft"": false, ""prerelease"": false, ""published_at"": ""2026-09-25T10:00:00Z"",
  ""assets"": [
    { ""name"": ""Terminals.exe"", ""size"": 10, ""browser_download_url"": ""https://example/Terminals.exe"" },
    { ""name"": ""Terminals-v4.0.15.zip"", ""size"": 20, ""digest"": ""sha256:ABC123"", ""browser_download_url"": ""https://example/Terminals-v4.0.15.zip"" },
    { ""name"": ""TerminalsSetup-v4.0.15.msi"", ""size"": 30, ""browser_download_url"": ""https://example/TerminalsSetup-v4.0.15.msi"" }
  ]
},
{ ""tag_name"": ""v4.0.14"", ""draft"": false, ""prerelease"": false, ""published_at"": ""2026-09-25T09:00:00Z"" }
]";

        private ReleaseInfo RunUpdateCheck()
        {
            const string CreateRss = @"
[
{
    ""name"": ""Title"",
    ""tag_name"": ""3.0.0"",
    ""published_at"": ""2017-06-14T20:25:15Z""
}
]";

            var updateManager = new UpdateManager(() => CreateRss);
            return updateManager.CheckForPublishedRelease(this.currentVersion);
        }

        private void AssertLastUpdateCheck()
        {
            DateTime lastNoticed = this.ParseLastUpdateDate();
            Assert.IsTrue(lastNoticed.Date > this.yesterDay, "Last update check date wasn't saved");
        }

        private DateTime ParseLastUpdateDate()
        {
            var updateChecksFile = new UpdateChecksFile();
            return updateChecksFile.ReadLastUpdate();
        }
    }
}
