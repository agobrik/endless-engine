using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Defines a discoverable unlock entry for the Unlock/Discovery Log.
    ///
    /// EntryCategory controls which section of the log UI this appears in.
    /// UnlockConditionHint is a player-facing hint shown before the entry is discovered.
    /// </summary>
    [CreateAssetMenu(menuName = "Endless Engine/Unlock Entry", fileName = "UnlockEntry")]
    public class UnlockEntryConfigSO : ScriptableObject
    {
        [Header("Identity")]
        public string EntryId;
        public string DisplayName;
        [TextArea(2, 3)]
        public string Description;
        public UnlockCategory Category = UnlockCategory.Item;

        [Header("Discovery")]
        [TextArea(1, 2)]
        public string UnlockConditionHint; // shown before unlock — e.g. "Reach Wave 10"
        public bool   IsHiddenUntilUnlocked = true;
    }

    public enum UnlockCategory
    {
        Item,
        Building,
        Pet,
        System,
        Milestone,
        Achievement,
        Story,
    }
}
