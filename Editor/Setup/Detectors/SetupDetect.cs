using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Wagenheimer.CloudSave.Editor.Setup.Detectors
{
    /// <summary>
    /// Shared, cheap readers for the project files detectors inspect. Phase 0 keeps this small;
    /// Phase 1 folds in the cached Assets/**/*.cs scanning from CloudSaveAudit.
    /// </summary>
    internal static class SetupDetect
    {
        public static string ReadTextOrNull(string projectRoot, string relativePath)
        {
            try
            {
                var full = Path.Combine(projectRoot, relativePath);
                return File.Exists(full) ? File.ReadAllText(full) : null;
            }
            catch { return null; }
        }

        /// <summary>Extracts a "key: value" scalar from a YAML-ish ProjectSettings file.</summary>
        public static string YamlScalar(string text, string key)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var m = Regex.Match(text, @"(?m)^\s*" + Regex.Escape(key) + @":\s*(.+?)\s*$");
            if (!m.Success) return null;
            var v = m.Groups[1].Value.Trim().Trim('"', '\'');
            return v.Length == 0 || v == "{}" ? null : v;
        }

        /// <summary>Returns the resolved version string of a UPM package from packages-lock.json / manifest.json, or null.</summary>
        public static string PackageVersion(string projectRoot, string packageId)
        {
            var lockText = ReadTextOrNull(projectRoot, "Packages/packages-lock.json");
            if (lockText != null)
            {
                var m = Regex.Match(lockText,
                    "\"" + Regex.Escape(packageId) + "\"\\s*:\\s*\\{[^}]*?\"version\"\\s*:\\s*\"([^\"]+)\"",
                    RegexOptions.Singleline);
                if (m.Success) return m.Groups[1].Value;
            }

            var manifest = ReadTextOrNull(projectRoot, "Packages/manifest.json");
            if (manifest != null)
            {
                var m = Regex.Match(manifest, "\"" + Regex.Escape(packageId) + "\"\\s*:\\s*\"([^\"]+)\"");
                if (m.Success) return m.Groups[1].Value;
            }
            return null;
        }

        public static bool PackagePresent(string projectRoot, string packageId)
            => !string.IsNullOrEmpty(PackageVersion(projectRoot, packageId));
    }
}
