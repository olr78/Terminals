using System;
using System.Linq;
using Newtonsoft.Json;

namespace Terminals.Updates
{
    /// <summary>
    /// GitHub release as returned by the releases REST API.
    /// </summary>
    public class Release
    {
        [JsonProperty("tag_name")]
        public string TagName { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("published_at")]
        public DateTime Published { get; set; }

        [JsonProperty("html_url")]
        public string HtmlUrl { get; set; }

        [JsonProperty("body")]
        public string Notes { get; set; }

        [JsonProperty("draft")]
        public bool Draft { get; set; }

        [JsonProperty("prerelease")]
        public bool Prerelease { get; set; }

        [JsonProperty("assets")]
        public ReleaseAsset[] Assets { get; set; }

        /// <summary>
        /// Gets version parsed from the tag name (e.g. "v4.0.14" or "4.0.14").
        /// Returns null, if the tag isn't a version.
        /// </summary>
        [JsonIgnore]
        public Version Version
        {
            get { return ParseVersion(this.TagName); }
        }

        internal static Version ParseVersion(string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
                return null;

            string versionText = tagName.Trim().TrimStart('v', 'V');
            Version parsed;
            if (!Version.TryParse(versionText, out parsed))
                return null;

            return Normalize(parsed);
        }

        /// <summary>
        /// Only major, minor and build are compared, because the assembly revision is generated during the build.
        /// </summary>
        internal static Version Normalize(Version version)
        {
            int build = Math.Max(version.Build, 0);
            return new Version(version.Major, version.Minor, build);
        }

        internal ReleaseAsset FindAsset(string extension)
        {
            if (this.Assets == null)
                return null;

            return this.Assets.FirstOrDefault(asset => asset.Name != null &&
                asset.Name.StartsWith("Terminals", StringComparison.OrdinalIgnoreCase) &&
                asset.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        }
    }
}
