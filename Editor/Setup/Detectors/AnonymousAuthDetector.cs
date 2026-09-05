using System.Collections.Generic;

namespace Wagenheimer.CloudSave.Editor.Setup.Detectors
{
    /// <summary>
    /// Detects the <c>com.unity.services.authentication</c> package (anonymous auth is on by default
    /// once the service is enabled). Fingerprint input: resolved authentication package version.
    /// </summary>
    public sealed class AnonymousAuthDetector : IStepDetector
    {
        public const string PackageId = "com.unity.services.authentication";
        public const string PackageVersionInput = "authentication.package.version";

        public ConfigurationReport Detect(SetupContext ctx)
        {
            var version = SetupDetect.PackageVersion(ctx.ProjectRoot, PackageId);
            var fp = new Dictionary<string, string> { [PackageVersionInput] = version ?? "" };

            if (string.IsNullOrEmpty(version))
                return new ConfigurationReport(
                    ConfigurationStatus.Missing,
                    missing: new[] { $"{PackageId} package" },
                    fingerprintValues: fp);

            return new ConfigurationReport(
                ConfigurationStatus.Present,
                found: new[] { $"{PackageId} {version}" },
                fingerprintValues: fp);
        }
    }
}
