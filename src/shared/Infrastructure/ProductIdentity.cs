using System;
using System.IO;

namespace Bimwright.Dwg.Plugin
{
    /// <summary>
    /// Product name, author, per-user data folder, environment-variable prefix and pipe prefix in one place.
    /// tbc-acad-mcp is a fork of bimwright/dwg-mcp: the legacy BIMWRIGHT_DWG_* names are kept here only so
    /// existing environment settings keep working. Code namespaces stay Bimwright.Dwg.* to keep upstream merges small.
    /// </summary>
    public static class ProductIdentity
    {
        public const string Name = "tbc-acad-mcp";
        public const string Author = "Nour Rouas";
        public const string AuthorLine = "Nour Rouas — BIM developer/modeller at TeamBIMCivil";
        public const string GitHubRepository = "desarrolloteambimcivil/tbc_mcp_autocad";
        public const string RepositoryUrl = "https://github.com/" + GitHubRepository;
        public const string UpstreamRepositoryUrl = "https://github.com/bimwright/dwg-mcp";

        public const string VendorFolder = "NourRouas";
        public const string ProductFolder = "tbc-acad-mcp";
        public const string EnvPrefix = "TBC_ACAD_MCP_";
        public const string LegacyEnvPrefix = "BIMWRIGHT_DWG_";

        /// <summary>Prefix of the plugin's named pipe: TbcAcadMcp-&lt;year&gt;-&lt;pid&gt;.</summary>
        public const string PipePrefix = "TbcAcadMcp-";

        /// <summary>Key prefix for the MCP <c>_meta</c> entries this gateway adds.</summary>
        public const string MetaKeyPrefix = GitHubRepository + "/";

        /// <summary>Folder holding the per-user data root, for code that combines the two parts itself.</summary>
        public static string VendorRoot(string localAppData = null)
            => Path.Combine(localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), VendorFolder);

        /// <summary>Per-user data root: %LOCALAPPDATA%\NourRouas\tbc-acad-mcp.</summary>
        public static string DataRoot(string localAppData = null)
            => Path.Combine(VendorRoot(localAppData), ProductFolder);

        /// <summary>
        /// Reads TBC_ACAD_MCP_X; when it is not set, falls back to the legacy BIMWRIGHT_DWG_X spelling.
        /// Names without the current prefix are read as given.
        /// </summary>
        public static string GetEnv(string name, Func<string, string> lookup = null)
        {
            lookup = lookup ?? Environment.GetEnvironmentVariable;
            var value = lookup(name);
            if (value != null || name == null || !name.StartsWith(EnvPrefix, StringComparison.Ordinal)) return value;
            return lookup(LegacyEnvPrefix + name.Substring(EnvPrefix.Length));
        }

        /// <summary>Wraps a lookup so every read gets the legacy fallback of <see cref="GetEnv"/>.</summary>
        public static Func<string, string> WithLegacyFallback(Func<string, string> lookup)
        {
            lookup = lookup ?? Environment.GetEnvironmentVariable;
            return name => GetEnv(name, lookup);
        }
    }
}
