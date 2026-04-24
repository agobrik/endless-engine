using System.Collections.Generic;
using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Registry of all realms available in the game.
    /// Contains all RealmPackSO references indexed by slug.
    /// Consumed by RealmConfigSystem to manage unlock state and swap orchestration.
    /// </summary>
    [CreateAssetMenu(fileName = "RealmRegistry", menuName = "Endless Engine/Config/Realm Registry")]
    public class RealmRegistrySO : ScriptableObject
    {
        [Tooltip("All realm packs. The first realm with IsDefaultRealm=true is available from the start.")]
        public RealmEntry[] Realms = new RealmEntry[0];

        /// <summary>Returns true if a realm with the given slug exists in the registry.</summary>
        public bool HasRealm(string slug)
        {
            foreach (var r in Realms)
                if (r.Slug == slug) return true;
            return false;
        }

        /// <summary>Returns the RealmEntry for a slug, or null.</summary>
        public RealmEntry GetEntry(string slug)
        {
            foreach (var r in Realms)
                if (r.Slug == slug) return r;
            return null;
        }

        /// <summary>Returns the default realm entry (IsDefaultRealm=true), or null.</summary>
        public RealmEntry GetDefaultRealm()
        {
            foreach (var r in Realms)
                if (r.IsDefaultRealm) return r;
            return null;
        }

        /// <summary>Returns the RealmPackSO for the given slug, or null.</summary>
        public RealmPackSO GetPack(string slug)
        {
            var entry = GetEntry(slug);
            return entry?.Pack;
        }

        /// <summary>All realm entries for display iteration.</summary>
        public IReadOnlyList<RealmEntry> AllRealms => Realms;
    }

    [System.Serializable]
    public class RealmEntry
    {
        public string   Slug;
        public string   DisplayName;
        public bool     IsDefaultRealm;
        public int      UnlockPrestigeThreshold;
        public RealmPackSO Pack;
        public Sprite   PreviewImage;
    }
}
