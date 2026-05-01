using System.Collections.Generic;

namespace EndlessEngine.Harvest
{
    /// <summary>
    /// Scene-lifetime registry of all live HarvestNode instances.
    /// HarvestLoopService queries this to find nodes overlapping the cursor.
    /// Static so any system can reach it without scene references.
    /// </summary>
    public static class HarvestNodeRegistry
    {
        private static readonly List<HarvestNode> _nodes = new();

        public static IReadOnlyList<HarvestNode> All => _nodes;

        internal static void Register(HarvestNode node)
        {
            if (!_nodes.Contains(node))
                _nodes.Add(node);
        }

        internal static void Unregister(HarvestNode node)
        {
            _nodes.Remove(node);
        }

        /// <summary>Clears all registrations. Call in test TearDown or scene unload.</summary>
        public static void Clear() => _nodes.Clear();
    }
}
