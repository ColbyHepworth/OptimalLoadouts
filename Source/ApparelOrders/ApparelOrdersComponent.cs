using System.Collections.Generic;
using System.Linq;
using OptimalLoadouts.Orders;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// Every apparel policy's stat order and forbids in the current game: saved with it, resolved with bases, and edited
    /// only through these methods so the resolved orders and the policies' checklists stay current. Saves record this
    /// class's full name, so it keeps it.
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
            ApparelForbids.ClearCache();
            NotifyChanged();
            // Items from a mod added since the last save are caught too.
            ApparelForbids.ApplyAll();
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
        /// The policy's lines, then its base's, then that base's, skipping a stat or item already listed. A base that
        /// was deleted or would loop back is ignored.
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
                // A deleted base takes its forbids with it, so what they unticked in policies built on it ticks back.
                ApparelForbids.ApplyAll();
            }
            if (resolved.TryGetValue(policy, out List<OrderLine> lines))
            {
                return lines;
            }
            lines = new List<OrderLine>();
            var visited = new HashSet<ApparelPolicy>();
            var listed = new HashSet<Def>();
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
                    if (line.Subject != null && listed.Add(line.Subject))
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
            ForbidsChanged();
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

        /// <summary>Pins the item: an item line placed above the policy's first stat line, after the items pinned before it.</summary>
        public void PreferItem(ApparelPolicy policy, ThingDef item)
        {
            ApparelOrder order = GetOrCreate(policy);
            if (order.lines.Any(line => line.item == item))
            {
                return;
            }
            int firstStat = order.lines.FindIndex(line => !line.IsItem);
            order.lines.Insert(firstStat < 0 ? order.lines.Count : firstStat, OrderLine.ForItem(item));
            NotifyChanged();
        }

        /// <summary>Unpins the item: removes the policy's own line for it. A base's line for it stays in the base.</summary>
        public void StopPreferring(ApparelPolicy policy, ThingDef item)
        {
            ApparelOrder order = OrderFor(policy);
            if (order != null && order.lines.RemoveAll(line => line.item == item) > 0)
            {
                NotifyChanged();
            }
        }

        /// <summary>Whether the item is pinned in the policy or its base.</summary>
        public bool Prefers(ApparelPolicy policy, ThingDef item)
        {
            IReadOnlyList<OrderLine> lines = EffectiveLines(policy);
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].item == item)
                {
                    return true;
                }
            }
            return false;
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

        /// <summary>The policy's own forbids, then its base's, then that base's. A base that would loop back is ignored.</summary>
        public List<ApparelForbid> EffectiveForbids(ApparelPolicy policy)
        {
            var forbids = new List<ApparelForbid>();
            var visited = new HashSet<ApparelPolicy>();
            for (ApparelPolicy current = policy; current != null && visited.Add(current); current = BaseOf(current))
            {
                ApparelOrder order = OrderFor(current);
                if (order == null)
                {
                    break;
                }
                forbids.AddRange(order.forbids);
            }
            return forbids;
        }

        /// <summary>Adds a forbid on the stat at the lowest value any apparel has, so it catches nothing until it's moved.</summary>
        public void AddForbid(ApparelPolicy policy, StatDef stat)
        {
            GetOrCreate(policy).forbids.Add(new ApparelForbid(stat, ApparelForbids.RangeOf(stat).min));
            ForbidsChanged();
        }

        public void RemoveForbid(ApparelPolicy policy, int index)
        {
            ApparelOrder order = OrderFor(policy);
            if (order == null || index < 0 || index >= order.forbids.Count)
            {
                return;
            }
            order.forbids.RemoveAt(index);
            ForbidsChanged();
        }

        /// <summary>Sets whether a forbid rules out values above or below its threshold, and the threshold.</summary>
        public void SetForbid(ApparelPolicy policy, int index, bool forbidAbove, float threshold)
        {
            ApparelOrder order = OrderFor(policy);
            if (order == null || index < 0 || index >= order.forbids.Count)
            {
                return;
            }
            ApparelForbid forbid = order.forbids[index];
            if (forbid.forbidAbove == forbidAbove && forbid.threshold == threshold)
            {
                return;
            }
            forbid.forbidAbove = forbidAbove;
            forbid.threshold = threshold;
            // A slider sends a new threshold each frame it's dragged. Only a changed checklist needs the preview redone.
            if (ApparelForbids.ApplyAll())
            {
                NotifyChanged();
            }
        }

        public void SetForbidLayer(ApparelPolicy policy, int index, ApparelLayerDef layer, bool allBut)
        {
            ApparelOrder order = OrderFor(policy);
            if (order == null || index < 0 || index >= order.forbids.Count)
            {
                return;
            }
            order.forbids[index].layer = layer;
            order.forbids[index].allButLayer = layer != null && allBut;
            ForbidsChanged();
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
            copy.forbids = source.forbids.Select(forbid => forbid.Copy()).ToList();
            // The game copies the checklist, so the copy's forbids unticked the same items.
            copy.unticked = new List<ThingDef>(source.unticked);
            copy.basePolicy = source.basePolicy != to ? source.basePolicy : null;
            ForbidsChanged();
        }

        /// <summary>Goes up whenever any order or base changes, so work done from the orders knows to start over.</summary>
        public int Version { get; private set; }

        /// <summary>Resolves the orders again and writes every policy's forbids into its checklist.</summary>
        private void ForbidsChanged()
        {
            NotifyChanged();
            ApparelForbids.ApplyAll();
        }

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
