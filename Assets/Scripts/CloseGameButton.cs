using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DefaultNamespace
{
    [RequireComponent(typeof(Button))]
    public class CloseGameButton : MonoBehaviour
    {
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(RestartGame);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(RestartGame);
            }
        }

        /// <summary>
        /// Перезагружает текущую активную сцену и сбрасывает Time.timeScale
        /// </summary>
        public void RestartGame()
        {
            // Возвращаем нормальную скорость времени (если игра была на паузе)
            Time.timeScale = 1f;

            // Перезагружаем текущую сцену заново
            string currentSceneName = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(currentSceneName);
        }
    }
}