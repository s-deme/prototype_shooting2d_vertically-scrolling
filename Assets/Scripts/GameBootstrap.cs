using UnityEngine;

namespace AliceMirrorfall
{
    /// <summary>Creates the game from code so the project has no prefab or external-asset dependency.</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureGameExists()
        {
            if (FindObjectOfType<WonderlandGame>() != null)
                return;

            var host = new GameObject("Alice Mirrorfall");
            DontDestroyOnLoad(host);
            host.AddComponent<WonderlandGame>();
        }
    }
}
