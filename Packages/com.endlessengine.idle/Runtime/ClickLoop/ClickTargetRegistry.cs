using System.Collections.Generic;

namespace EndlessEngine.ClickLoop
{
    /// <summary>
    /// Scene-lifetime registry of all live ClickTarget instances.
    /// ClickLoopService queries this to find the target under the pointer.
    /// </summary>
    public static class ClickTargetRegistry
    {
        private static readonly List<ClickTarget> _targets = new();

        public static IReadOnlyList<ClickTarget> All => _targets;

        internal static void Register(ClickTarget t)
        {
            if (!_targets.Contains(t)) _targets.Add(t);
        }

        internal static void Unregister(ClickTarget t) => _targets.Remove(t);

        public static void Clear() => _targets.Clear();
    }
}
