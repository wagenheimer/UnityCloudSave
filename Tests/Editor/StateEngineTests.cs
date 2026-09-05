using System;
using System.Collections.Generic;
using NUnit.Framework;
using Wagenheimer.CloudSave.Editor.Setup;

namespace Wagenheimer.CloudSave.Editor.Setup.Tests
{
    public class StateEngineTests
    {
        static readonly SetupContext Ctx = new("C:/does/not/matter");
        static readonly string[] Fp = { "input" };

        static StepDefinition Step(
            string id = "s",
            bool runtime = false,
            bool manual = false,
            IReadOnlyList<DependencyEdge> deps = null,
            Func<SetupContext, bool> appliesTo = null,
            IReadOnlyList<string> fingerprintInputs = null)
            => new(id, id, StepCategory.Services, Obligation.Required, new StepCopy(),
                appliesTo, deps, fingerprintInputs ?? Fp, manual, runtime);

        static StepEvaluation Eval(
            StepDefinition def, ConfigurationStatus config,
            IPersistedState persisted = null,
            IReadOnlyDictionary<string, string> fingerprint = null,
            Func<string, StepEvaluation> resolveDep = null)
            => StateEngine.Evaluate(def, Ctx, new FakeDetector(config, fingerprint),
                persisted ?? new FakePersistedState(), Array.Empty<string>(), resolveDep ?? (_ => null));

        [Test]
        public void Missing_config_is_NotConfigured()
            => Assert.AreEqual(StepState.NotConfigured, Eval(Step(), ConfigurationStatus.Missing).State);

        [Test]
        public void Partial_config_is_NeedsAttention()
            => Assert.AreEqual(StepState.NeedsAttention, Eval(Step(), ConfigurationStatus.Partial).State);

        [Test]
        public void Present_config_no_runtime_no_manual_is_Validated()
            => Assert.AreEqual(StepState.Validated, Eval(Step(runtime: false), ConfigurationStatus.Present).State);

        [Test]
        public void Present_with_runtime_but_no_record_is_NeedsValidation()
            => Assert.AreEqual(StepState.NeedsValidation, Eval(Step(runtime: true), ConfigurationStatus.Present).State);

        [Test]
        public void Present_with_passing_record_and_matching_fingerprint_is_Validated()
        {
            var fp = new Dictionary<string, string> { ["input"] = "v1" };
            var expected = Fingerprint.Compute(Fp, fp);
            var persisted = new FakePersistedState().WithRecord("s", ValidationOutcomeName.Passed, expected);
            Assert.AreEqual(StepState.Validated, Eval(Step(runtime: true), ConfigurationStatus.Present, persisted, fp).State);
        }

        [Test]
        public void Present_with_failing_record_is_Failed()
        {
            var persisted = new FakePersistedState().WithRecord("s", ValidationOutcomeName.Failed, "whatever");
            Assert.AreEqual(StepState.Failed, Eval(Step(runtime: true), ConfigurationStatus.Present, persisted).State);
        }

        [Test]
        public void Passing_record_with_stale_fingerprint_is_NeedsValidation_and_Stale()
        {
            var oldFp = Fingerprint.Compute(Fp, new Dictionary<string, string> { ["input"] = "OLD" });
            var nowFp = new Dictionary<string, string> { ["input"] = "NEW" };
            var persisted = new FakePersistedState().WithRecord("s", ValidationOutcomeName.Passed, oldFp);

            var e = Eval(Step(runtime: true), ConfigurationStatus.Present, persisted, nowFp);

            Assert.AreEqual(RuntimeVerificationStatus.Stale, e.Runtime);
            Assert.AreEqual(StepState.NeedsValidation, e.State);
            Assert.IsTrue(e.RuntimeStale);
        }

        [Test]
        public void Manual_only_step_confirmed_is_ManuallyConfirmed_not_Validated()
        {
            var persisted = new FakePersistedState().WithManualConfirmed("s");
            var e = Eval(Step(runtime: false, manual: true), ConfigurationStatus.Present, persisted);
            Assert.AreEqual(StepState.ManuallyConfirmed, e.State);
        }

        [Test]
        public void Manual_only_step_unconfirmed_is_NeedsValidation()
        {
            var e = Eval(Step(runtime: false, manual: true), ConfigurationStatus.Present);
            Assert.AreEqual(StepState.NeedsValidation, e.State);
        }

        [Test]
        public void Unmet_dependency_makes_step_Blocked()
        {
            var dep = new DependencyEdge("up", DependencyGate.RequiresValidated, cascade: false);
            var upstream = Eval(Step("up"), ConfigurationStatus.Missing); // NotConfigured
            var e = Eval(Step("s", deps: new[] { dep }), ConfigurationStatus.Present,
                resolveDep: id => id == "up" ? upstream : null);

            Assert.AreEqual(StepState.Blocked, e.State);
            Assert.AreEqual("up", e.BlockedByStepId);
        }

        [Test]
        public void Met_dependency_does_not_block()
        {
            var dep = new DependencyEdge("up", DependencyGate.RequiresConfigured, cascade: false);
            var upstream = Eval(Step("up", runtime: false), ConfigurationStatus.Present); // Validated
            var e = Eval(Step("s", deps: new[] { dep }), ConfigurationStatus.Present,
                resolveDep: id => id == "up" ? upstream : null);

            Assert.AreNotEqual(StepState.Blocked, e.State);
        }

        [Test]
        public void Out_of_scope_step_is_NotApplicable()
        {
            var e = Eval(Step("s", appliesTo: _ => false), ConfigurationStatus.Present);
            Assert.AreEqual(StepState.NotApplicable, e.State);
            Assert.IsFalse(e.Applicable);
        }

        [Test]
        public void Cascade_invalidation_makes_a_passing_step_stale_when_upstream_regresses()
        {
            var fp = new Dictionary<string, string> { ["input"] = "v1" };
            var goodFp = Fingerprint.Compute(Fp, fp);
            var persisted = new FakePersistedState().WithRecord("s", ValidationOutcomeName.Passed, goodFp);

            // upstream is only configured (NeedsValidation), not Validated
            var upstream = Eval(Step("up", runtime: true), ConfigurationStatus.Present);
            Assume.That(upstream.State, Is.EqualTo(StepState.NeedsValidation));

            var dep = new DependencyEdge("up", DependencyGate.RequiresConfigured, cascade: true);
            var e = Eval(Step("s", runtime: true, deps: new[] { dep }), ConfigurationStatus.Present, persisted, fp,
                resolveDep: id => id == "up" ? upstream : null);

            Assert.AreEqual(RuntimeVerificationStatus.Stale, e.Runtime);
        }
    }
}
