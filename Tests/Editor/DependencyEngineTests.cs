using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wagenheimer.CloudSave.Editor.Setup;

namespace Wagenheimer.CloudSave.Editor.Setup.Tests
{
    public class DependencyEngineTests
    {
        static readonly SetupContext Ctx = new("C:/x");

        static StepDefinition Def(string id, Obligation ob = Obligation.Required, params DependencyEdge[] deps)
            => new(id, id, StepCategory.Services, ob, new StepCopy(), null, deps, Array.Empty<string>());

        static DependencyEdge On(string id, DependencyGate gate = DependencyGate.RequiresValidated)
            => new(id, gate, cascade: false);

        [Test]
        public void Topological_order_places_dependencies_first()
        {
            var steps = new List<StepDefinition>
            {
                Def("c", Obligation.Required, On("b")),
                Def("b", Obligation.Required, On("a")),
                Def("a"),
            };

            var order = DependencyEngine.TopologicalOrder(steps).Select(s => s.Id).ToList();

            Assert.Less(order.IndexOf("a"), order.IndexOf("b"));
            Assert.Less(order.IndexOf("b"), order.IndexOf("c"));
        }

        [Test]
        public void Cycle_throws_named_exception()
        {
            var steps = new List<StepDefinition>
            {
                Def("a", Obligation.Required, On("b")),
                Def("b", Obligation.Required, On("a")),
            };

            var ex = Assert.Throws<CyclicDependencyException>(() => DependencyEngine.TopologicalOrder(steps));
            StringAssert.Contains("a", ex.Message);
            StringAssert.Contains("b", ex.Message);
        }

        [Test]
        public void Edges_to_unknown_ids_are_ignored()
        {
            var steps = new List<StepDefinition> { Def("a", Obligation.Required, On("ghost")) };
            Assert.DoesNotThrow(() => DependencyEngine.TopologicalOrder(steps));
        }

        // ── NextBestAction ──────────────────────────────────────────────────

        static StepEvaluation FakeEval(StepDefinition def, StepState state, params DependencyResolution[] deps)
        {
            // Build a real StepEvaluation via StateEngine using a detector/persisted crafted to yield `state`.
            // Simpler: use reflection-free path — StateEngine with a fake detector for the non-blocked states,
            // and an unmet dependency for Blocked.
            return state switch
            {
                StepState.Validated => StateEngine.Evaluate(def, Ctx, new FakeDetector(ConfigurationStatus.Present),
                    new FakePersistedState(), Array.Empty<string>(), _ => null),
                StepState.NotConfigured => StateEngine.Evaluate(def, Ctx, new FakeDetector(ConfigurationStatus.Missing),
                    new FakePersistedState(), Array.Empty<string>(), _ => null),
                StepState.NeedsValidation => StateEngine.Evaluate(
                    WithRuntime(def), Ctx, new FakeDetector(ConfigurationStatus.Present),
                    new FakePersistedState(), Array.Empty<string>(), _ => null),
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        static StepDefinition WithRuntime(StepDefinition d)
            => new(d.Id, d.Title, d.Category, d.Obligation, d.Copy, null, d.DependsOn, new[] { "fp" },
                hasManualRequirement: false, hasRuntimeValidator: true);

        [Test]
        public void NextBestAction_prefers_step_that_unblocks_the_most()
        {
            var a = Def("a");                    // actionable, unblocks b and c
            var d = Def("d");                    // actionable, unblocks nothing
            var b = Def("b", Obligation.Required, On("a"));
            var c = Def("c", Obligation.Required, On("a"));

            var evals = new List<StepEvaluation>
            {
                FakeEval(a, StepState.NeedsValidation),
                FakeEval(d, StepState.NeedsValidation),
                FakeEval(b, StepState.NotConfigured),
                FakeEval(c, StepState.NotConfigured),
            };

            var next = DependencyEngine.PickNextAction(evals);

            Assert.IsNotNull(next);
            Assert.AreEqual("a", next.Step.Definition.Id);
            Assert.AreEqual(2, next.UnblocksCount);
        }

        [Test]
        public void NextBestAction_is_null_when_everything_is_done()
        {
            var evals = new List<StepEvaluation>
            {
                FakeEval(Def("a"), StepState.Validated),
                FakeEval(Def("b"), StepState.Validated),
            };

            Assert.IsNull(DependencyEngine.PickNextAction(evals));
        }

        [Test]
        public void NextBestAction_skips_blocked_steps_and_returns_their_root()
        {
            var a = Def("a");
            var b = Def("b", Obligation.Required, On("a"));

            var aEval = FakeEval(a, StepState.NeedsValidation);
            var bEval = StateEngine.Evaluate(WithRuntime(b), Ctx, new FakeDetector(ConfigurationStatus.Present),
                new FakePersistedState(), Array.Empty<string>(),
                id => id == "a" ? aEval : null); // a is NeedsValidation, gate RequiresValidated → b blocked

            Assume.That(bEval.State, Is.EqualTo(StepState.Blocked));

            var next = DependencyEngine.PickNextAction(new List<StepEvaluation> { aEval, bEval });
            Assert.AreEqual("a", next.Step.Definition.Id);
        }
    }
}
