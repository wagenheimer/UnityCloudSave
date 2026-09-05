using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Wagenheimer.CloudSave.Editor.Setup
{
    /// <summary>
    /// One recorded runtime validation pass, bound to the configuration fingerprint it ran against.
    /// If the step's fingerprint later differs, the record is stale and the step needs re-validation.
    /// </summary>
    [Serializable]
    public sealed class ValidationRecord
    {
        public string StepId;
        public string CaseId;
        public string Outcome;        // ValidationOutcome name
        public string Fingerprint;    // step config fingerprint at run time
        public string StartedAtUtc;   // ISO-8601
        public long DurationMs;
        public string PackageVersion;
        public string UgsEnvironment;
        public string Message;
    }

    /// <summary>
    /// A developer's confirmation of one external-console item that the system cannot machine-verify.
    /// "Confirmed" is a claim, not a verification.
    /// </summary>
    [Serializable]
    public sealed class ManualConfirmation
    {
        public string StepId;
        public string ItemId;
        public bool Confirmed;
        public string ConfirmedAtUtc;
    }

    /// <summary>
    /// Read side of the persisted state, so the engines can be unit-tested without a ScriptableObject.
    /// </summary>
    public interface IPersistedState
    {
        ValidationRecord LatestRecordFor(string stepId);
        bool IsStepManuallyConfirmed(string stepId, IReadOnlyList<string> itemIds);
    }

    /// <summary>
    /// The ONLY persisted state: runtime validation records + manual confirmations. Everything else
    /// (configuration status, step state, meters, readiness) is recomputed live on every refresh.
    /// Stored as a versioned asset so it travels with the project and other devs see the same picture.
    /// </summary>
    public sealed class CloudSaveSetupState : ScriptableObject, IPersistedState
    {
        public const string AssetPath = "Assets/CloudSave/CloudSaveSetup.asset";

        [SerializeField] List<ValidationRecord> _records = new();
        [SerializeField] List<ManualConfirmation> _manual = new();

        public IReadOnlyList<ValidationRecord> Records => _records;
        public IReadOnlyList<ManualConfirmation> ManualConfirmations => _manual;

        public ValidationRecord LatestRecordFor(string stepId)
        {
            ValidationRecord latest = null;
            foreach (var r in _records)
            {
                if (r.StepId != stepId) continue;
                if (latest == null || string.CompareOrdinal(r.StartedAtUtc, latest.StartedAtUtc) > 0)
                    latest = r;
            }
            return latest;
        }

        public void RecordValidation(ValidationRecord record)
        {
            _records.Add(record);
            Save();
        }

        public bool IsManuallyConfirmed(string stepId, string itemId)
        {
            var m = _manual.FirstOrDefault(x => x.StepId == stepId && x.ItemId == itemId);
            return m is { Confirmed: true };
        }

        public bool IsStepManuallyConfirmed(string stepId, IReadOnlyList<string> itemIds)
        {
            if (itemIds == null || itemIds.Count == 0) return false;
            return itemIds.All(id => IsManuallyConfirmed(stepId, id));
        }

        public void SetManualConfirmation(string stepId, string itemId, bool confirmed)
        {
            var m = _manual.FirstOrDefault(x => x.StepId == stepId && x.ItemId == itemId);
            if (m == null)
            {
                m = new ManualConfirmation { StepId = stepId, ItemId = itemId };
                _manual.Add(m);
            }
            m.Confirmed = confirmed;
            m.ConfirmedAtUtc = confirmed ? DateTime.UtcNow.ToString("o") : null;
            Save();
        }

        void Save()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Loads the asset, creating it (and the Assets/CloudSave folder) on first use.</summary>
        public static CloudSaveSetupState GetOrCreate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<CloudSaveSetupState>(AssetPath);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder("Assets/CloudSave"))
                AssetDatabase.CreateFolder("Assets", "CloudSave");

            var created = CreateInstance<CloudSaveSetupState>();
            AssetDatabase.CreateAsset(created, AssetPath);
            AssetDatabase.SaveAssets();
            return created;
        }
    }
}
