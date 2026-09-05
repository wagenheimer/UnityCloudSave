using System.Collections.Generic;

namespace Wagenheimer.CloudSave.Editor.Setup.Detectors
{
    /// <summary>
    /// Detects whether the project is linked to a Unity Cloud project (org + project id in
    /// ProjectSettings). Fingerprint inputs: cloudProjectId, organizationId.
    /// </summary>
    public sealed class UgsProjectDetector : IStepDetector
    {
        public const string CloudProjectId = "cloudProjectId";
        public const string OrganizationId = "organizationId";

        public ConfigurationReport Detect(SetupContext ctx)
        {
            var ps = SetupDetect.ReadTextOrNull(ctx.ProjectRoot, "ProjectSettings/ProjectSettings.asset");
            var projectId = SetupDetect.YamlScalar(ps, CloudProjectId);
            var orgId = SetupDetect.YamlScalar(ps, OrganizationId);

            var fp = new Dictionary<string, string>
            {
                [CloudProjectId] = projectId ?? "",
                [OrganizationId] = orgId ?? "",
            };

            var found = new List<string>();
            var missing = new List<string>();

            if (!string.IsNullOrEmpty(projectId)) found.Add($"Cloud project id: {projectId}");
            else missing.Add("cloudProjectId (Project Settings → Services → link a project)");

            if (!string.IsNullOrEmpty(orgId)) found.Add($"Organization: {orgId}");
            else missing.Add("organizationId");

            var status = (!string.IsNullOrEmpty(projectId), !string.IsNullOrEmpty(orgId)) switch
            {
                (true, true) => ConfigurationStatus.Present,
                (false, false) => ConfigurationStatus.Missing,
                _ => ConfigurationStatus.Partial,
            };

            return new ConfigurationReport(status, found, missing, fp);
        }
    }
}
