using System;
using System.Collections.Generic;
using System.Linq;

namespace Wagenheimer.CloudSave.Editor.Setup
{
    public sealed class CyclicDependencyException : Exception
    {
        public CyclicDependencyException(string cyclePath)
            : base($"Cloud Save setup steps contain a dependency cycle: {cyclePath}. " +
                   "This is an authoring bug in the step registry — break the loop.") { }
    }

    /// <summary>The single most important thing to do next, with a plain-language reason.</summary>
    public sealed class NextBestAction
    {
        public StepEvaluation Step { get; }
        public string Why { get; }
        public int UnblocksCount { get; }

        public NextBestAction(StepEvaluation step, string why, int unblocksCount)
        {
            Step = step;
            Why = why;
            UnblocksCount = unblocksCount;
        }
    }

    /// <summary>
    /// Graph algorithms over the step DAG: build-time cycle detection, topological ordering
    /// (so the State Engine can resolve dependencies in one pass), and Next Best Action ranking.
    /// </summary>
    public static class DependencyEngine
    {
        /// <summary>
        /// Kahn topological sort. Throws <see cref="CyclicDependencyException"/> naming the loop.
        /// Edges to unknown step ids are ignored (a step may depend on something not in this set).
        /// </summary>
        public static IReadOnlyList<StepDefinition> TopologicalOrder(IReadOnlyList<StepDefinition> steps)
        {
            var byId = steps.ToDictionary(s => s.Id);
            var indegree = steps.ToDictionary(s => s.Id, s => s.DependsOn.Count(e => byId.ContainsKey(e.DependsOnId)));
            var dependents = steps.ToDictionary(s => s.Id, _ => new List<string>());
            foreach (var s in steps)
                foreach (var e in s.DependsOn)
                    if (byId.ContainsKey(e.DependsOnId))
                        dependents[e.DependsOnId].Add(s.Id);

            // Stable: seed the queue in registry order.
            var queue = new Queue<string>(steps.Where(s => indegree[s.Id] == 0).Select(s => s.Id));
            var order = new List<StepDefinition>(steps.Count);

            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                order.Add(byId[id]);
                foreach (var d in dependents[id])
                    if (--indegree[d] == 0)
                        queue.Enqueue(d);
            }

            if (order.Count != steps.Count)
            {
                var remaining = steps.Where(s => order.All(o => o.Id != s.Id)).Select(s => s.Id).ToList();
                throw new CyclicDependencyException(FindCycle(remaining, byId));
            }

            return order;
        }

        static string FindCycle(List<string> nodes, Dictionary<string, StepDefinition> byId)
        {
            var set = new HashSet<string>(nodes);
            var stack = new List<string>();
            var onStack = new HashSet<string>();

            string Dfs(string id)
            {
                if (onStack.Contains(id))
                {
                    var start = stack.IndexOf(id);
                    return string.Join(" → ", stack.Skip(start).Append(id));
                }
                if (!set.Contains(id)) return null;
                stack.Add(id);
                onStack.Add(id);
                foreach (var e in byId[id].DependsOn)
                {
                    var found = Dfs(e.DependsOnId);
                    if (found != null) return found;
                }
                stack.RemoveAt(stack.Count - 1);
                onStack.Remove(id);
                return null;
            }

            foreach (var n in nodes)
            {
                var found = Dfs(n);
                if (found != null) return found;
            }
            return string.Join(", ", nodes);
        }

        static readonly StepState[] ActionableStates =
        {
            StepState.NotConfigured, StepState.NeedsAttention, StepState.NeedsValidation, StepState.Failed,
        };

        /// <summary>
        /// Ranks actionable steps (dependencies met, not done, not blocked) and returns the top one.
        /// Order: obligation ▸ how many steps it unblocks ▸ category order ▸ registry order.
        /// </summary>
        public static NextBestAction PickNextAction(IReadOnlyList<StepEvaluation> evaluations)
        {
            var index = new Dictionary<string, int>();
            for (int i = 0; i < evaluations.Count; i++) index[evaluations[i].Definition.Id] = i;

            int UnblocksCount(string stepId) => evaluations.Count(e =>
                e.Definition.DependsOn.Any(d => d.DependsOnId == stepId) &&
                (e.State == StepState.Blocked || ActionableStates.Contains(e.State)));

            var actionable = evaluations.Where(e =>
                e.Applicable &&
                ActionableStates.Contains(e.State) &&
                e.Dependencies.All(d => d.Met)).ToList();

            if (actionable.Count == 0) return null;

            var best = actionable
                .OrderBy(e => (int)e.Definition.Obligation)
                .ThenByDescending(e => UnblocksCount(e.Definition.Id))
                .ThenBy(e => (int)e.Definition.Category)
                .ThenBy(e => index[e.Definition.Id])
                .First();

            int unblocks = UnblocksCount(best.Definition.Id);
            return new NextBestAction(best, BuildWhy(best, evaluations, unblocks), unblocks);
        }

        static string BuildWhy(StepEvaluation step, IReadOnlyList<StepEvaluation> all, int unblocks)
        {
            if (unblocks > 0)
            {
                var names = all
                    .Where(e => e.Definition.DependsOn.Any(d => d.DependsOnId == step.Definition.Id))
                    .Select(e => e.Definition.Title)
                    .Take(3)
                    .ToList();
                var list = string.Join(", ", names);
                return $"\"{step.Definition.Title}\" must be done before {list}" +
                       (unblocks > names.Count ? $" (and {unblocks - names.Count} more)." : ".");
            }

            return step.State == StepState.Failed
                ? $"\"{step.Definition.Title}\" was tested and failed — fix it before continuing."
                : $"\"{step.Definition.Title}\" is the next {step.Definition.Obligation.ToString().ToLowerInvariant()} step.";
        }
    }
}
