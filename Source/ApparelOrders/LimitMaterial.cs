using System.Collections.Generic;
using RimWorld;
using Verse;

namespace OptimalLoadouts.ApparelOrders
{
    /// <summary>
    /// Which material a policy's limits judge apparel in: its usual material, the best one for each limit, or one the
    /// player picked. Items that aren't made of a material are judged as they are.
    /// </summary>
    public sealed class LimitMaterial
    {
        /// <summary>The game's default material for each item.</summary>
        public static readonly LimitMaterial Usual = new LimitMaterial(null, false);

        /// <summary>For each limit, the material that does best on its stat: an item passes if any material it can be made of does.</summary>
        public static readonly LimitMaterial Best = new LimitMaterial(null, true);

        private static readonly Dictionary<ThingDef, LimitMaterial> specific = new Dictionary<ThingDef, LimitMaterial>();

        private LimitMaterial(ThingDef stuff, bool isBest)
        {
            Stuff = stuff;
            IsBest = isBest;
        }

        /// <summary>The material picked, or null for usual and best.</summary>
        public ThingDef Stuff { get; }

        public bool IsBest { get; }

        /// <summary>This material for items that can be made of it, and the usual one for the rest.</summary>
        public static LimitMaterial Of(ThingDef stuff)
        {
            if (stuff == null)
            {
                return Usual;
            }
            if (!specific.TryGetValue(stuff, out LimitMaterial material))
            {
                material = new LimitMaterial(stuff, false);
                specific[stuff] = material;
            }
            return material;
        }

        /// <summary>The material an item is judged in, or null for an item not made of one. Not for <see cref="Best"/>, which tries them all.</summary>
        public ThingDef StuffFor(ThingDef def)
        {
            if (!def.MadeFromStuff)
            {
                return null;
            }
            return Stuff != null && Stuff.stuffProps.CanMake(def) ? Stuff : GenStuff.DefaultStuffFor(def);
        }
    }
}
