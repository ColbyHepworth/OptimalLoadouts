using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// Every apparel policy's stat order in the current game: saved with it, resolved with bases, and edited only
    /// through these methods so the resolved orders stay current. Saves record this class's full name, so it keeps it.
    /// </summary>
    public class ApparelOrdersComponent : GameComponent
    {
        private static readonly List<OrderLine> NoLines = new List<OrderLine>();

        private List<ApparelOrder> orders = new List<ApparelOrder>();
        private readonly Dictionary<ApparelPolicy, List<OrderLine>> resolved = new Dictionary<ApparelPolicy, List<OrderLine>>();
        private readonly List<ApparelPolicy> resolvedForPolicies = new List<ApparelPolicy>();

        public ApparelOrdersComponent(Game game)
        {
        }

        /// <summary>The current game's orders, or null outside a game.</summary>
        public static ApparelOrdersComponent Current => Verse.Current.Game?.GetComponent<ApparelOrdersComponent>();

        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                orders.RemoveAll(order => order.policy == null || !PolicyExists(order.policy));
                foreach (ApparelOrder order in orders)
                {
                    if (order.basePolicy != null && !PolicyExists(order.basePolicy))
                    {
                        order.basePolicy = null;
                    }
                }
            }
            Scribe_Collections.Look(ref orders, "orders", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (orders == null)
                {
                    orders = new List<ApparelOrder>();
                }
                orders.RemoveAll(order => order == null || order.policy == null);
                NotifyChanged();
            }
        }

        public override void FinalizeInit()
        {
            PieceStats.ClearCache();
            NotifyChanged();
        }

        /// <summary>The policy's own order, or null when it has none.</summary>
        public ApparelOrder OrderFor(ApparelPolicy policy)
        {
            if (policy == null)
            {
                return null;
            }
            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i].policy == policy)
                {
                    return orders[i];
                }
            }
            return null;
        }

        private ApparelOrder GetOrCreate(ApparelPolicy policy)
        {
            ApparelOrder order = OrderFor(policy);
            if (order == null)
            {
                order = new ApparelOrder { policy = policy };
                orders.Add(order);
            }
            return order;
        }

        /// <summary>
        /// The policy's lines, then its base's, then that base's, skipping a stat already listed. A base that was
        /// deleted or would loop back is ignored.
        /// </summary>
        public IReadOnlyList<OrderLine> EffectiveLines(ApparelPolicy policy)
        {
            if (policy == null)
            {
                return NoLines;
            }
            // Policies are added and deleted in the game's own window, so a changed list is the sign to resolve again.
            List<ApparelPolicy> policies = Verse.Current.Game.outfitDatabase.AllOutfits;
            if (!SamePolicies(policies))
            {
                NotifyChanged();
                resolvedForPolicies.Clear();
                resolvedForPolicies.AddRange(policies);
            }
            if (resolved.TryGetValue(policy, out List<OrderLine> lines))
            {
                return lines;
            }
            lines = new List<OrderLine>();
            var visited = new HashSet<ApparelPolicy>();
            var listedStats = new HashSet<StatDef>();
            ApparelPolicy current = policy;
            while (current != null && visited.Add(current))
            {
                ApparelOrder order = OrderFor(current);
                if (order == null)
                {
                    break;
                }
                foreach (OrderLine line in order.lines)
                {
                    if (line.stat != null && listedStats.Add(line.stat))
                    {
                        lines.Add(line);
                    }
                }
                current = BaseOf(current);
            }
            resolved[policy] = lines;
            return lines;
        }

        /// <summary>The policy's base, or null when it has none or the base no longer exists.</summary>
        public ApparelPolicy BaseOf(ApparelPolicy policy)
        {
            ApparelPolicy basePolicy = OrderFor(policy)?.basePolicy;
            return basePolicy != null && PolicyExists(basePolicy) ? basePolicy : null;
        }

        /// <summary>Whether a policy can be another's base: not itself, not one built on it, and still there.</summary>
        public bool CanBeBase(ApparelPolicy policy, ApparelPolicy basePolicy)
        {
            if (basePolicy == null || basePolicy == policy || !PolicyExists(basePolicy))
            {
                return false;
            }
            var visited = new HashSet<ApparelPolicy>();
            for (ApparelPolicy below = basePolicy; below != null && visited.Add(below); below = BaseOf(below))
            {
                if (below == policy)
                {
                    return false;
                }
            }
            return true;
        }

        public void SetBase(ApparelPolicy policy, ApparelPolicy basePolicy)
        {
            if (basePolicy != null && !CanBeBase(policy, basePolicy))
            {
                return;
            }
            GetOrCreate(policy).basePolicy = basePolicy;
            NotifyChanged();
        }

        public void AddLine(ApparelPolicy policy, StatDef stat)
        {
            ApparelOrder order = GetOrCreate(policy);
            if (order.lines.Any(line => line.stat == stat))
            {
                return;
            }
            // A stat the policy gets from its base keeps the base's layer and direction when it becomes the policy's own.
            OrderLine fromBase = EffectiveLines(policy).FirstOrDefault(line => line.stat == stat);
            order.lines.Add(fromBase?.Copy() ?? new OrderLine(stat));
            NotifyChanged();
        }

        public void RemoveLine(ApparelPolicy policy, int index)
        {
            ApparelOrder order = OrderFor(policy);
            if (order == null || index < 0 || index >= order.lines.Count)
            {
                return;
            }
            order.lines.RemoveAt(index);
            NotifyChanged();
        }

        /// <summary>Moves a line up (negative) or down (positive) by one place.</summary>
        public void MoveLine(ApparelPolicy policy, int index, int direction)
        {
            ApparelOrder order = OrderFor(policy);
            int target = index + direction;
            if (order == null || index < 0 || index >= order.lines.Count || target < 0 || target >= order.lines.Count)
            {
                return;
            }
            OrderLine line = order.lines[index];
            order.lines[index] = order.lines[target];
            order.lines[target] = line;
            NotifyChanged();
        }

        public void ToggleDirection(ApparelPolicy policy, int index)
        {
            ApparelOrder order = OrderFor(policy);
            if (order == null || index < 0 || index >= order.lines.Count)
            {
                return;
            }
            order.lines[index].lowerIsBetter = !order.lines[index].lowerIsBetter;
            NotifyChanged();
        }

        /// <summary>Limits a line to a layer, or to every layer but it, or (with no layer) lets it count on every layer.</summary>
        public void SetLayer(ApparelPolicy policy, int index, ApparelLayerDef layer, bool allBut)
        {
            ApparelOrder order = OrderFor(policy);
            if (order == null || index < 0 || index >= order.lines.Count)
            {
                return;
            }
            order.lines[index].layer = layer;
            order.lines[index].allButLayer = layer != null && allBut;
            NotifyChanged();
        }

        /// <summary>Gives a duplicated policy the same order and base as the policy it was copied from.</summary>
        public void CopyOrder(ApparelPolicy from, ApparelPolicy to)
        {
            if (from == null || to == null || from == to)
            {
                return;
            }
            ApparelOrder source = OrderFor(from);
            if (source == null)
            {
                orders.RemoveAll(order => order.policy == to);
                NotifyChanged();
                return;
            }
            ApparelOrder copy = GetOrCreate(to);
            copy.lines = source.lines.Select(line => line.Copy()).ToList();
            copy.basePolicy = source.basePolicy != to ? source.basePolicy : null;
            NotifyChanged();
        }

        /// <summary>Goes up whenever any order or base changes, so work done from the orders knows to start over.</summary>
        public int Version { get; private set; }

        /// <summary>Forgets the resolved orders, so the next lookup reads the current lines and bases.</summary>
        public void NotifyChanged()
        {
            resolved.Clear();
            Version++;
        }

        private bool SamePolicies(List<ApparelPolicy> policies)
        {
            if (policies.Count != resolvedForPolicies.Count)
            {
                return false;
            }
            for (int i = 0; i < policies.Count; i++)
            {
                if (policies[i] != resolvedForPolicies[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static bool PolicyExists(ApparelPolicy policy)
        {
            return Verse.Current.Game.outfitDatabase.AllOutfits.Contains(policy);
        }
    }
}
