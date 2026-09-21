using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DefaultNamespace
{
    public class GameLoader : MonoBehaviour
    {
        private void Awake()
        {
            Button button = GetComponent<Button>();
            button.onClick.AddListener(LoadGame);
        }

        private void LoadGame()
        {
            SceneManager.LoadScene("Scenes/MainGame");
        }
    }
}