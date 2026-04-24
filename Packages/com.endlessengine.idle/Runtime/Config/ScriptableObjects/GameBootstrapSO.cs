using UnityEngine;

namespace EndlessEngine.Config
{
    /// <summary>
    /// Root Addressable asset loaded at boot via address "game-bootstrap".
    /// Points to the active RealmPackSO whose 8 canonical SO types are loaded next.
    /// Store the handle on ConfigLoadingService — never release during a normal session.
    /// </summary>
    [CreateAssetMenu(fileName = "GameBootstrap", menuName = "Endless Engine/Config/Game Bootstrap")]
    public class GameBootstrapSO : ScriptableObject
    {
        [Tooltip("The realm pack to load on boot. Must reference all 8 canonical SO types.")]
        public RealmPackSO ActiveRealmPack;
    }
}
