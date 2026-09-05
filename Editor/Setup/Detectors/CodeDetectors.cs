using System.Collections.Generic;
using System.Linq;

namespace Wagenheimer.CloudSave.Editor.Setup.Detectors
{
    /// <summary>
    /// Generic "is this API called anywhere in the project?" detector. Fingerprint is coarse
    /// (present / absent): remove the call and any downstream validation goes stale.
    /// </summary>
    public sealed class CodePresenceDetector : IStepDetector
    {
        readonly string _pattern;
        readonly string _fingerprintKey;
        readonly string _foundLabel;
        readonly string _missingHint;
        readonly bool _partialWhenAbsent;

        public CodePresenceDetector(string pattern, string fingerprintKey, string foundLabel, string missingHint,
            bool partialWhenAbsent = false)
        {
            _pattern = pattern;
            _fingerprintKey = fingerprintKey;
            _foundLabel = foundLabel;
            _missingHint = missingHint;
            _partialWhenAbsent = partialWhenAbsent;
        }

        public ConfigurationReport Detect(SetupContext ctx)
        {
            var hits = ctx.Code.Find(_pattern);
            bool present = hits.Count > 0;
            var fp = new Dictionary<string, string> { [_fingerprintKey] = present ? "yes" : "no" };

            if (present)
            {
                var found = new List<string> { _foundLabel };
                found.AddRange(hits.Take(4));
                return new ConfigurationReport(ConfigurationStatus.Present, found, null, fp);
            }

            return new ConfigurationReport(
                _partialWhenAbsent ? ConfigurationStatus.Partial : ConfigurationStatus.Missing,
                missing: new[] { _missingHint }, fingerprintValues: fp);
        }
    }

    /// <summary>
    /// A <c>long LastSaved / SaveDateTime</c> field on the save class — the timestamp the SDK's
    /// last-write-wins conflict resolution compares.
    /// </summary>
    public sealed class TimestampFieldDetector : IStepDetector
    {
        public ConfigurationReport Detect(SetupContext ctx)
        {
            var strong = ctx.Code.Find(@"long\s+(LastSaved|SaveDateTime|LastSaveTime)");
            if (strong.Count > 0)
                return new ConfigurationReport(ConfigurationStatus.Present,
                    found: strong.Take(3).Prepend("Timestamp field found"),
                    fingerprintValues: One("save.timestamp", "long"));

            var weak = ctx.Code.Find(@"\b(LastSaved|SaveDateTime|LastSaveTime)\b");
            if (weak.Count > 0)
                return new ConfigurationReport(ConfigurationStatus.Partial,
                    found: weak.Take(3).Prepend("A timestamp-like field exists — confirm its type is `long`"),
                    missing: new[] { "public long LastSaved; (set to DateTime.UtcNow.Ticks)" },
                    fingerprintValues: One("save.timestamp", "weak"));

            return new ConfigurationReport(ConfigurationStatus.Missing,
                missing: new[] { "Your save class needs: public long LastSaved; (DateTime.UtcNow.Ticks)" },
                fingerprintValues: One("save.timestamp", "no"));
        }

        static Dictionary<string, string> One(string k, string v) => new() { [k] = v };
    }

    /// <summary>
    /// Legacy backend migration. Present when CloudMigration.TryMigrateAsync is wired; a bare
    /// PlayFab/Firebase reference with no migration wiring is a Partial (needs attention);
    /// a clean UGS-only project is Present ("nothing to migrate").
    /// </summary>
    public sealed class LegacyMigrationDetector : IStepDetector
    {
        public ConfigurationReport Detect(SetupContext ctx)
        {
            if (ctx.Code.Any(@"CloudMigration\.TryMigrateAsync"))
                return new ConfigurationReport(ConfigurationStatus.Present,
                    found: ctx.Code.Find(@"CloudMigration\.TryMigrateAsync").Take(3).Prepend("Migration wired"),
                    fingerprintValues: new Dictionary<string, string> { ["migration"] = "wired" });

            var legacy = ctx.Code.Find(@"PlayFabClientAPI|\bPlayFab\b|\bFirebase\b");
            if (legacy.Count > 0)
                return new ConfigurationReport(ConfigurationStatus.Partial,
                    found: legacy.Take(3).Prepend("Legacy backend referenced — no CloudMigration wiring found"),
                    missing: new[] { "Wire CloudMigration.TryMigrateAsync (see Samples~/PlayFabMigration)" },
                    fingerprintValues: new Dictionary<string, string> { ["migration"] = "legacy-unwired" });

            return new ConfigurationReport(ConfigurationStatus.Present,
                found: new[] { "No legacy backend detected — nothing to migrate" },
                fingerprintValues: new Dictionary<string, string> { ["migration"] = "none" });
        }
    }

    /// <summary>Android Google Play Games: the plugin package or PlayGamesPlatform usage.</summary>
    public sealed class GpgsPresenceDetector : IStepDetector
    {
        public ConfigurationReport Detect(SetupContext ctx)
        {
            var pkg = SetupDetect.PackageVersion(ctx.ProjectRoot, "com.google.play.games");
            var code = ctx.Code.Find(@"PlayGamesPlatform|GooglePlayGames|NativeSocial\.");
            bool present = !string.IsNullOrEmpty(pkg) || code.Count > 0;

            var fp = new Dictionary<string, string> { ["gpgs.pkg"] = pkg ?? "", ["gpgs.code"] = code.Count > 0 ? "yes" : "no" };
            if (!present)
                return new ConfigurationReport(ConfigurationStatus.Missing,
                    missing: new[] { "com.google.play.games plugin + PlayGamesPlatform auth flow (Android only)" },
                    fingerprintValues: fp);

            var found = new List<string>();
            if (!string.IsNullOrEmpty(pkg)) found.Add($"com.google.play.games {pkg}");
            found.AddRange(code.Take(3));
            return new ConfigurationReport(ConfigurationStatus.Present, found, null, fp);
        }
    }
}
