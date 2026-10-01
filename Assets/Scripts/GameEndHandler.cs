using UnityEngine;

public class GameEndHandler : MonoBehaviour
{
    [Header("Win UI")]
    [Tooltip("Объект Pop-up окна победы")]
    [SerializeField] private GameObject winPopupUI;

    [Header("Lose VFX Settings")]
    [Tooltip("Префабы или объекты ParticleSystem взрывов для проигрыша")]
    [SerializeField] private ParticleSystem[] explosionVFXs;

    [Tooltip("Точки на сцене, где произойдут взрывы (если пусто, взрывы родятся в позиции данного скрипта)")]
    [SerializeField] private Transform[] explosionSpawnPoints;

    private void OnEnable()
    {
        PipesColorController.OnGameWon += HandleGameWon;
        PipesColorController.OnGameLost += HandleGameLost;
    }

    private void OnDisable()
    {
        PipesColorController.OnGameWon -= HandleGameWon;
        PipesColorController.OnGameLost -= HandleGameLost;
    }

    private void HandleGameWon()
    {
        Debug.Log("[GameEndHandler] Обработка события ПОБЕДЫ.");

        if (winPopupUI != null)
        {
            winPopupUI.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Win Popup UI не назначен в инспекторе!");
        }
    }

    private void HandleGameLost()
    {
        Debug.Log("[GameEndHandler] Обработка события ПОРАЖЕНИЯ.");

        // Запуск VFX взрывов
        if (explosionVFXs != null && explosionVFXs.Length > 0)
        {
            for (int i = 0; i < explosionVFXs.Length; i++)
            {
                ParticleSystem vfxPrefab = explosionVFXs[i];
                if (vfxPrefab == null) continue;

                Vector3 spawnPosition = transform.position;
                Quaternion spawnRotation = Quaternion.identity;

                if (explosionSpawnPoints != null && i < explosionSpawnPoints.Length && explosionSpawnPoints[i] != null)
                {
                    spawnPosition = explosionSpawnPoints[i].position;
                    spawnRotation = explosionSpawnPoints[i].rotation;
                }

                // Спавним VFX или заново запускаем, если они уже на сцене
                if (vfxPrefab.gameObject.scene.name != null)
                {
                    vfxPrefab.Play();
                }
                else
                {
                    ParticleSystem spawnedVFX = Instantiate(vfxPrefab, spawnPosition, spawnRotation);
                    spawnedVFX.Play();
                    Destroy(spawnedVFX.gameObject, spawnedVFX.main.duration + 1f);
                }
            }
        }
        else
        {
            Debug.LogWarning("VFX взрывов не назначены в инспекторе GameEndHandler!");
        }
    }
}