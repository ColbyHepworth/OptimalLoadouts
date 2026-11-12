using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders.Preview
{
    /// <summary>
    /// Makes the pawn's worn list hold an imagined outfit until disposed, then puts the real list back in its order.
    /// Nothing is put on or taken off, so no comps, hediffs, thoughts or other mods hear about it. Code that reads the
    /// worn list in the meantime, such as the apparel check's score and the pawn's drawing, sees the imagined outfit.
    /// </summary>
    public readonly struct ImaginedOutfit : IDisposable
    {
        private readonly List<Apparel> worn;
        private readonly List<Apparel> real;

        public ImaginedOutfit(Pawn pawn, IEnumerable<Apparel> outfit)
        {
            worn = pawn.apparel.WornApparel;
            real = new List<Apparel>(worn);
            worn.Clear();
            worn.AddRange(outfit);
            // In the order the game keeps worn apparel in, so the drawing stacks it the same way (its own sort is private).
            worn.Sort((a, b) => a.def.apparel.LastLayer.drawOrder.CompareTo(b.def.apparel.LastLayer.drawOrder));
        }

        public void Dispose()
        {
            worn.Clear();
            worn.AddRange(real);
        }
    }
}
