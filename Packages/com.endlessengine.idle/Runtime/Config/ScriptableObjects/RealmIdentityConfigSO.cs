using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Per-realm identity configuration. Defines arena bounds, display info,
    /// and unlock requirements. ArenaBounds is consumed by Enemy AI and physics movement.
    /// </summary>
    [CreateAssetMenu(fileName = "RealmIdentityConfig_Base", menuName = "Endless Engine/Config/Realm Identity")]
    public class RealmIdentityConfigSO : ScriptableObject
    {
        [Header("Identity")]
        public string RealmSlug = "base";
        public string DisplayName = "Base Realm";

        [Header("Arena")]
        [Tooltip("World-space rectangle defining the arena playfield. Read by Enemy AI and Physics Movement.")]
        public Rect ArenaBounds = new Rect(-10f, -6f, 20f, 12f);

        [Header("Unlock")]
        [Tooltip("Number of prestiges required to unlock this realm. 0 = unlocked from start.")]
        public int UnlockPrestigeRequired = 0;
    }
}
