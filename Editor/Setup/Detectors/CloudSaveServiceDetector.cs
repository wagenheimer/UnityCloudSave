using System.Collections.Generic;

namespace Wagenheimer.CloudSave.Editor.Setup.Detectors
{
    /// <summary>
    /// Detects the <c>com.unity.services.cloudsave</c> package. Whether the Cloud Save service is
    /// actually toggled on in the Unity Dashboard is a manual/runtime concern (proven by the
    /// anonymous verification step, and later by the Save round-trip in Phase 2).
    /// Fingerprint input: the resolved cloudsave package version.
    /// </summary>
    public sealed class CloudSaveServiceDetector : IStepDetector
    {
        public const string PackageId = "com.unity.services.cloudsave";
        public const string PackageVersionInput = "cloudsave.package.version";

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
