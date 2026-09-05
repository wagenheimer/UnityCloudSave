using System.Collections.Generic;
using Wagenheimer.CloudSave.Editor.Setup;

namespace Wagenheimer.CloudSave.Editor.Setup.Tests
{
    internal sealed class FakeDetector : IStepDetector
    {
        readonly ConfigurationReport _report;
        public FakeDetector(ConfigurationStatus status, IReadOnlyDictionary<string, string> fingerprint = null)
            => _report = new ConfigurationReport(status, fingerprintValues: fingerprint);
        public ConfigurationReport Detect(SetupContext ctx) => _report;
    }

    internal sealed class FakePersistedState : IPersistedState
    {
        readonly Dictionary<string, ValidationRecord> _records = new();
        readonly HashSet<string> _confirmedSteps = new();

        public FakePersistedState WithRecord(string stepId, ValidationOutcomeName outcome, string fingerprint)
        {
            _records[stepId] = new ValidationRecord
            {
                StepId = stepId,
                Outcome = outcome.ToString(),
                Fingerprint = fingerprint,
                StartedAtUtc = System.DateTime.UtcNow.ToString("o"),
            };
            return this;
        }

        public FakePersistedState WithManualConfirmed(string stepId)
        {
            _confirmedSteps.Add(stepId);
            return this;
        }

        public ValidationRecord LatestRecordFor(string stepId)
            => _records.TryGetValue(stepId, out var r) ? r : null;

        public bool IsStepManuallyConfirmed(string stepId, IReadOnlyList<string> itemIds)
            => _confirmedSteps.Contains(stepId);
    }

    // Mirrors Wagenheimer.CloudSave.Verification.ValidationOutcome without depending on it in tests.
    internal enum ValidationOutcomeName { Passed, Failed, Inconclusive, Blocked, Skipped }
}
