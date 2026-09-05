using System.Collections.Generic;

namespace Wagenheimer.CloudSave.Editor.Setup.Detectors
{
    /// <summary>
    /// The "anonymous sign-in verified" step has nothing of its own to configure — its readiness is
    /// gated by dependencies (UGS project + Cloud Save + Authentication). It always reports Present so
    /// that, once unblocked, its state is driven purely by the runtime validation result.
    ///
    /// Its fingerprint folds in the identity-affecting inputs (project id, org, auth package version):
    /// change any of them and a previous PASS becomes Stale → NeedsValidation.
    /// </summary>
    public sealed class AnonymousVerificationDetector : IStepDetector
    {
        public ConfigurationReport Detect(SetupContext ctx)
        {
            var ps = SetupDetect.ReadTextOrNull(ctx.ProjectRoot, "ProjectSettings/ProjectSettings.asset");
            var fp = new Dictionary<string, string>
            {
                [UgsProjectDetector.CloudProjectId] = SetupDetect.YamlScalar(ps, UgsProjectDetector.CloudProjectId) ?? "",
                [UgsProjectDetector.OrganizationId] = SetupDetect.YamlScalar(ps, UgsProjectDetector.OrganizationId) ?? "",
                [AnonymousAuthDetector.PackageVersionInput] =
                    SetupDetect.PackageVersion(ctx.ProjectRoot, AnonymousAuthDetector.PackageId) ?? "",
            };

            return new ConfigurationReport(
                ConfigurationStatus.Present,
                found: new[] { "Ready to run the anonymous sign-in check once dependencies are met." },
                fingerprintValues: fp);
        }
    }
}
