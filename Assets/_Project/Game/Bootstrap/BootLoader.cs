using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleStudio.Game.Bootstrap
{
    /// <summary>Placed in Boot.unity (first scene of the build): loads the pack, then the Game scene.</summary>
    public sealed class BootLoader : MonoBehaviour
    {
        public const string GameScene = "Game";

        void Start()
        {
            GameBootstrap.EnsureInitialized();
            SceneManager.LoadScene(GameScene);
        }
    }
}
