using Newtonsoft.Json;

namespace Terminals.Updates
{
    /// <summary>
    /// Downloadable file attached to the GitHub release.
    /// </summary>
    public class ReleaseAsset
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("browser_download_url")]
        public string DownloadUrl { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        /// <summary>
        /// Checksum calculated by GitHub in form "sha256:hex".
        /// </summary>
        [JsonProperty("digest")]
        public string Digest { get; set; }

        /// <summary>
        /// Gets expected SHA-256 hash of the file as lowercase hex string or null, if not provided.
        /// </summary>
        [JsonIgnore]
        public string Sha256
        {
            get
            {
                const string PREFIX = "sha256:";
                if (string.IsNullOrEmpty(this.Digest) || !this.Digest.StartsWith(PREFIX))
                    return null;

                return this.Digest.Substring(PREFIX.Length).ToLowerInvariant();
            }
        }
    }
}
