using System.Collections.Generic;
using System.Linq;

namespace Wagenheimer.CloudSave.Editor.Setup.Detectors
{
    /// <summary>
    /// A UI step is satisfied if EITHER the package UI is used (a <c>&lt;Name&gt;.Create()</c> call or a
    /// prefab at <c>Assets/Resources/&lt;Name&gt;.prefab</c>) OR the project registered its own form for
    /// that hook in <see cref="CloudSaveSetupState"/>.
    /// </summary>
    public sealed class CustomUiDetector : IStepDetector
    {
        readonly string _createPattern;      // e.g. CloudSaveUI\.Create
        readonly string _resourceName;       // e.g. CloudSaveUI
        readonly UiHook _hook;
        readonly string _fingerprintKey;
        readonly string _optionalHint;

        public CustomUiDetector(string createPattern, string resourceName, UiHook hook, string fingerprintKey, string optionalHint)
        {
            _createPattern = createPattern;
            _resourceName = resourceName;
            _hook = hook;
            _fingerprintKey = fingerprintKey;
            _optionalHint = optionalHint;
        }

        public ConfigurationReport Detect(SetupContext ctx)
        {
            var custom = ctx.CustomUis.FirstOrDefault(u => u.Hook == _hook);
            bool createCalled = ctx.Code.Any(_createPattern);
            bool prefabExists = SetupDetect.ReadTextOrNull(ctx.ProjectRoot, $"Assets/Resources/{_resourceName}.prefab") != null;

            string mode = custom != null ? "custom:" + custom.PrefabPath
                : createCalled ? "package"
                : prefabExists ? "prefab-only"
                : "none";
            var fp = new Dictionary<string, string> { [_fingerprintKey] = mode };

            if (custom != null)
                return new ConfigurationReport(ConfigurationStatus.Present,
                    found: new[] { $"Covered by your form: {custom.DisplayName}" +
                                   (string.IsNullOrEmpty(custom.PrefabPath) ? "" : $"  ({custom.PrefabPath})") },
                    fingerprintValues: fp);

            if (createCalled)
                return new ConfigurationReport(ConfigurationStatus.Present,
                    found: ctx.Code.Find(_createPattern).Take(2).Prepend($"{_resourceName}.Create() called"),
                    fingerprintValues: fp);

            if (prefabExists)
                return new ConfigurationReport(ConfigurationStatus.Partial,
                    found: new[] { $"Assets/Resources/{_resourceName}.prefab exists but nothing calls {_resourceName}.Create()" },
                    missing: new[] { $"Call {_resourceName}.Create() at startup, or register your own form in the UI tab." },
                    fingerprintValues: fp);

            return new ConfigurationReport(ConfigurationStatus.Missing,
                missing: new[] { _optionalHint },
                fingerprintValues: fp);
        }
    }
}
