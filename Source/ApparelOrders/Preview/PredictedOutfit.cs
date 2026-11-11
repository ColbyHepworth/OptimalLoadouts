using System.Collections.Generic;
using OptimalLoadouts.Orders;
using RimWorld;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>The outfit a pawn would end up in under a policy, and the swaps that get it there.</summary>
    public sealed class PredictedOutfit
    {
        /// <summary>The final outfit.</summary>
        public IReadOnlyList<Apparel> Worn { get; }

        /// <summary>Pieces in the final outfit the pawn doesn't wear now.</summary>
        public IReadOnlyList<Apparel> PutOn { get; }

        /// <summary>Pieces the pawn wears now and won't in the end.</summary>
        public IReadOnlyList<Apparel> TakenOff { get; }

        /// <summary>Pieces taken off because the policy doesn't allow them, before any swap.</summary>
        public IReadOnlyList<Apparel> NotAllowed { get; }

        /// <summary>Each piece put on, in order.</summary>
        public IReadOnlyList<PredictedSwap> Swaps { get; }

        /// <summary>False when the pawn was still finding better pieces at the swap limit.</summary>
        public bool Settled { get; }

        /// <summary>True when the source offered no piece the policy allows.</summary>
        public bool NothingToPick { get; }

        public PredictedOutfit(IReadOnlyList<Apparel> worn, IReadOnlyList<Apparel> putOn, IReadOnlyList<Apparel> takenOff,
            IReadOnlyList<Apparel> notAllowed, IReadOnlyList<PredictedSwap> swaps, bool settled, bool nothingToPick)
        {
            Worn = worn;
            PutOn = putOn;
            TakenOff = takenOff;
            NotAllowed = notAllowed;
            Swaps = swaps;
            Settled = settled;
            NothingToPick = nothingToPick;
        }
    }

    /// <summary>One piece put on: what it replaced, why it won, and the piece that came closest.</summary>
    public sealed class PredictedSwap
    {
        public Apparel PutOn { get; }
        public IReadOnlyList<Apparel> Replaced { get; }
        public float Score { get; }
        public OrderVerdict Verdict { get; }

        /// <summary>The next best piece the pawn could have put on at this swap, or null when there was none.</summary>
        public Apparel RunnerUp { get; }

        public OrderVerdict RunnerUpVerdict { get; }

        public PredictedSwap(Apparel putOn, IReadOnlyList<Apparel> replaced, float score, OrderVerdict verdict, Apparel runnerUp,
            OrderVerdict runnerUpVerdict)
        {
            PutOn = putOn;
            Replaced = replaced;
            Score = score;
            Verdict = verdict;
            RunnerUp = runnerUp;
            RunnerUpVerdict = runnerUpVerdict;
        }
    }
}
