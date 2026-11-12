using System.Collections.Generic;
using RimWorld;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>One piece a slot could hold, as the slot browser lists it.</summary>
    public sealed class SlotChoice
    {
        /// <summary>The piece, or one of several identical stored copies.</summary>
        public Apparel Piece { get; }

        /// <summary>How many identical stored pieces it stands for. Always 1 for imagined pieces.</summary>
        public int Copies { get; }

        /// <summary>Whether the policy's filter allows the item type, which is what its checkbox sets.</summary>
        public bool TypeAllowed { get; }

        /// <summary>Whether the policy's filter allows this piece: its type, and also its quality, condition and special filters.</summary>
        public bool Allowed { get; }

        /// <summary>False for a stored piece the pawn can't reach, or on an outfit stand that's switched off.</summary>
        public bool Reachable { get; }

        /// <summary>The apparel check's score with the order applied, for the slot left empty.</summary>
        public float Score { get; }

        /// <summary>True when the score is under the apparel check's threshold, so the game wouldn't put it on.</summary>
        public bool GameWontWear => Score < OutfitPredictor.MinimumGain;

        /// <summary>Why the game never puts it on this pawn by itself (it blocks vision, it's for slaves), or null.</summary>
        public string CantWearReason { get; }

        /// <summary>Whether the pawn would consider it at all: allowed, reachable and scored high enough.</summary>
        public bool Pickable => Allowed && Reachable && !GameWontWear;

        /// <summary>Its place in the pawn's choice for the slot, 1 being the pick, whatever the list is sorted by. 0 when not pickable.</summary>
        public int PickRank { get; internal set; }

        /// <summary>Worn pieces outside this slot that it would replace too.</summary>
        public IReadOnlyList<Apparel> AlsoReplaces { get; }

        public SlotChoice(Apparel piece, int copies, bool typeAllowed, bool allowed, bool reachable, float score,
            string cantWearReason, IReadOnlyList<Apparel> alsoReplaces)
        {
            Piece = piece;
            Copies = copies;
            TypeAllowed = typeAllowed;
            Allowed = allowed;
            Reachable = reachable;
            Score = score;
            CantWearReason = cantWearReason;
            AlsoReplaces = alsoReplaces;
        }
    }
}
