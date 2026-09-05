using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Wagenheimer.CloudSave.Editor.Setup.Detectors
{
    /// <summary>
    /// Cached line-by-line scan of the consumer project's <c>Assets/**/*.cs</c>. Ported from
    /// CloudSaveAudit's "70x faster" file cache so the many code-presence detectors share one read.
    /// A <see cref="SetupSnapshot"/> recompute builds the cache once and clears it when done.
    /// </summary>
    public sealed class CodeScan
    {
        readonly Dictionary<string, string[]> _files = new();
        readonly Dictionary<string, Regex> _regexCache = new();

        CodeScan() { }

        public static CodeScan Build(string projectRoot)
        {
            var scan = new CodeScan();
            try
            {
                var assets = Path.Combine(projectRoot, "Assets");
                if (!Directory.Exists(assets)) return scan;
                foreach (var file in Directory.EnumerateFiles(assets, "*.cs", SearchOption.AllDirectories))
                {
                    try
                    {
                        var rel = file.Substring(projectRoot.Length + 1).Replace('\\', '/');
                        scan._files[rel] = File.ReadAllLines(file);
                    }
                    catch { /* unreadable file — skip */ }
                }
            }
            catch { /* Assets missing (running inside the package repo) — empty scan */ }
            return scan;
        }

        Regex Rx(string pattern)
        {
            if (!_regexCache.TryGetValue(pattern, out var rx))
                _regexCache[pattern] = rx = new Regex(pattern, RegexOptions.Compiled);
            return rx;
        }

        /// <summary>All "relpath:line" hits for a pattern (capped).</summary>
        public List<string> Find(string pattern, int max = 8)
        {
            var rx = Rx(pattern);
            var hits = new List<string>();
            foreach (var kv in _files)
            {
                var lines = kv.Value;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!rx.IsMatch(lines[i])) continue;
                    hits.Add($"{kv.Key}:{i + 1}  {lines[i].Trim()}");
                    if (hits.Count >= max) return hits;
                }
            }
            return hits;
        }

        public bool Any(string pattern)
        {
            var rx = Rx(pattern);
            foreach (var kv in _files)
                foreach (var line in kv.Value)
                    if (rx.IsMatch(line))
                        return true;
            return false;
        }

        public int FileCount => _files.Count;
    }
}
