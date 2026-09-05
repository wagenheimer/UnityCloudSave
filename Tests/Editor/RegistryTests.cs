using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wagenheimer.CloudSave.Editor.Setup;

namespace Wagenheimer.CloudSave.Editor.Setup.Tests
{
    public class RegistryTests
    {
        [Test]
        public void Registry_builds_without_dependency_cycle_or_duplicate_ids()
        {
            Assert.DoesNotThrow(() => new SetupRegistry());
        }

        [Test]
        public void Every_step_has_a_detector_and_unique_id()
        {
            var reg = new SetupRegistry();
            var ids = reg.Steps.Select(s => s.Id).ToList();

            CollectionAssert.AllItemsAreUnique(ids);
            foreach (var s in reg.Steps)
                Assert.IsNotNull(reg.DetectorFor(s.Id), $"no detector for {s.Id}");
        }

        [Test]
        public void Every_dependency_points_at_a_real_step()
        {
            var reg = new SetupRegistry();
            var ids = new HashSet<string>(reg.Steps.Select(s => s.Id));

            foreach (var s in reg.Steps)
                foreach (var edge in s.DependsOn)
                    Assert.IsTrue(ids.Contains(edge.DependsOnId), $"{s.Id} depends on unknown {edge.DependsOnId}");
        }

        [Test]
        public void Compute_over_an_empty_project_produces_a_full_snapshot_without_throwing()
        {
            var reg = new SetupRegistry();
            var ctx = new SetupContext("C:/definitely/not/a/unity/project");

            SetupSnapshot snap = null;
            Assert.DoesNotThrow(() => snap = SetupModel.Compute(reg, ctx, new FakePersistedState()));

            Assert.AreEqual(reg.Steps.Count, snap.Steps.Count);
            // Nothing configured, nothing verified — not production ready.
            Assert.AreEqual(ReadinessVerdict.Red, snap.Readiness);
            Assert.IsNotNull(snap.NextAction, "there should always be a next action on a fresh project");
        }
    }
}
