using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Wagenheimer.CloudSave.Editor.Setup.Detectors;

namespace Wagenheimer.CloudSave.Editor.Setup
{
    /// <summary>
    /// Ambient facts detectors share for one recompute: the project root and a one-shot cached
    /// scan of Assets/**/*.cs (so the many code-presence detectors read the disk only once).
    /// </summary>
    public sealed class SetupContext
    {
        /// <summary>Absolute path to the consumer project root (parent of Assets/ and Packages/).</summary>
        public string ProjectRoot { get; }

        /// <summary>Cached line scan of the project's C# — never null (empty when Assets/ is absent).</summary>
        public CodeScan Code { get; }

        public SetupContext(string projectRoot, CodeScan code = null)
        {
            ProjectRoot = projectRoot;
            Code = code ?? CodeScan.Build(projectRoot);
        }

        public static SetupContext ForCurrentProject()
        {
            // Application.dataPath is <project>/Assets
            var assets = UnityEngine.Application.dataPath;
            var root = System.IO.Directory.GetParent(assets)?.FullName ?? assets;
            return new SetupContext(root);
        }
    }

    /// <summary>
    /// What a detector returns: the status plus human-readable evidence and the raw fingerprint inputs
    /// (input name → current value) declared by the step's <see cref="StepDefinition.FingerprintInputs"/>.
    /// </summary>
    public sealed class ConfigurationReport
    {
        public ConfigurationStatus Status { get; }
        public IReadOnlyList<string> Found { get; }
        public IReadOnlyList<string> Missing { get; }
        public IReadOnlyDictionary<string, string> FingerprintValues { get; }

        public ConfigurationReport(
            ConfigurationStatus status,
            IEnumerable<string> found = null,
            IEnumerable<string> missing = null,
            IReadOnlyDictionary<string, string> fingerprintValues = null)
        {
            Status = status;
            Found = (found ?? Enumerable.Empty<string>()).ToArray();
            Missing = (missing ?? Enumerable.Empty<string>()).ToArray();
            FingerprintValues = fingerprintValues ?? new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Single responsibility: inspect the project and report whether a step's configuration is present.
    /// Never runs anything, never mutates anything.
    /// </summary>
    public interface IStepDetector
    {
        ConfigurationReport Detect(SetupContext ctx);
    }

    /// <summary>
    /// Deterministic hash of a step's declared fingerprint inputs. Two evaluations with the same
    /// input values produce the same fingerprint; any change flips runtime status to Stale.
    /// </summary>
    public static class Fingerprint
    {
        public static string Compute(IReadOnlyList<string> inputNames, IReadOnlyDictionary<string, string> values)
        {
            if (inputNames == null || inputNames.Count == 0) return "";

            var sb = new StringBuilder();
            foreach (var name in inputNames.OrderBy(n => n, StringComparer.Ordinal))
            {
                values.TryGetValue(name, out var v);
                sb.Append(name).Append('=').Append(v ?? "\0").Append('\n');
            }

            using var sha = SHA1.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            var hex = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) hex.Append(b.ToString("x2"));
            return hex.ToString();
        }
    }
}
