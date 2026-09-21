using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PipesColorController : MonoBehaviour
{
    // Singleton для доступа из сцены мини-игры
    public static PipesColorController Instance { get; private set; }

    public enum PipeStatus
    {
        Normal,     // Бирюзовый - нет поломок
        Warning,    // Жёлтый - маленькие проблемы
        Broken      // Красный - поломано
    }

    [Header("Pipes Root / Renderers")]
    [Tooltip("Родительский объект или список MeshRenderer всех труб")]
    [SerializeField] private MeshRenderer[] pipeRenderers;

    [Header("HDR Emission Colors")]
    [ColorUsage(true, true)] [SerializeField] private Color normalColor = new Color(0f, 1f, 1f, 1f) * 2f;
    [ColorUsage(true, true)] [SerializeField] private Color warningColor = new Color(1f, 0.92f, 0.016f, 1f) * 2f;
    [ColorUsage(true, true)] [SerializeField] private Color brokenColor = new Color(1f, 0f, 0f, 1f) * 2f;

    [Header("Interaction & Minigame Settings")]
    [SerializeField] private Camera mainCamera;
    [Tooltip("Точное имя сцены мини-игры или путь (например: Scenes/MiniGame)")]
    [SerializeField] private string minigameSceneName = "Scenes/MiniGame";

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    private List<Material> instantiatedMaterials = new List<Material>();
    private Dictionary<MeshRenderer, int> rendererToIndexMap = new Dictionary<MeshRenderer, int>();

    private int randomBrokenIndex = -1;
    private bool isSystemActive = false;
    private bool isMinigameActive = false; // Блокировка повторных кликов
    private int currentInteractingPipeIndex = -1;

    private void Awake()
    {
        // Singleton паттерн
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (pipeRenderers == null || pipeRenderers.Length == 0)
            pipeRenderers = GetComponentsInChildren<MeshRenderer>();

        // Создаем индивидуальные копии материалов для каждой трубы
        for (int i = 0; i < pipeRenderers.Length; i++)
        {
            MeshRenderer rend = pipeRenderers[i];
            if (rend != null)
            {
                Material instanceMat = rend.material;
                instantiatedMaterials.Add(instanceMat);
                rendererToIndexMap[rend] = i;

                instanceMat.DisableKeyword("_EMISSION");
                instanceMat.SetColor(EmissionColorID, Color.black);
            }
        }

        // Выбираем случайную поломанную трубу при старте
        if (instantiatedMaterials.Count > 0)
        {
            randomBrokenIndex = Random.Range(0, instantiatedMaterials.Count);
        }
    }

    private void Update()
    {
        // Не принимаем клики, если система выключена или мини-игра УЖЕ запущена
        if (!isSystemActive || isMinigameActive) return;

        // Клик ЛКМ через New Input System
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryRepairPipeUnderCursor();
        }
    }

    /// <summary>
    /// Вызывается из WaterControlTool при нажатии кнопки UI
    /// </summary>
    public void SetPipesColor(bool isToggled)
    {
        isSystemActive = isToggled;
        if (instantiatedMaterials.Count == 0) return;

        for (int i = 0; i < instantiatedMaterials.Count; i++)
        {
            Material mat = instantiatedMaterials[i];
            if (mat == null) continue;

            if (isToggled)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor(EmissionColorID, i == randomBrokenIndex ? brokenColor : normalColor);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor(EmissionColorID, Color.black);
            }
        }
    }

    private void TryRepairPipeUnderCursor()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.TryGetComponent<MeshRenderer>(out MeshRenderer hitRenderer))
            {
                if (rendererToIndexMap.TryGetValue(hitRenderer, out int pipeIndex))
                {
                    // Если кликнули по поломанной (красной) трубе — запускаем мини-игру
                    if (pipeIndex == randomBrokenIndex)
                    {
                        currentInteractingPipeIndex = pipeIndex;
                        StartMinigame();
                    }
                }
            }
        }
    }

    private void StartMinigame()
    {
        isMinigameActive = true; // Блокируем клики по 3D миру
        SceneManager.LoadScene(minigameSceneName, LoadSceneMode.Additive);
    }

    /// <summary>
    /// Этот метод вызывает PipeMinigame.cs при завершении игры
    /// </summary>
    public void OnMinigameCompleted(bool success)
    {
        SceneManager.UnloadSceneAsync(minigameSceneName);

        if (success && currentInteractingPipeIndex != -1)
        {
            RepairPipe(currentInteractingPipeIndex);
        }

        currentInteractingPipeIndex = -1;
        isMinigameActive = false; // Разблокируем клики после закрытия мини-игры
    }

    public void RepairPipe(int pipeIndex)
    {
        if (pipeIndex < 0 || pipeIndex >= instantiatedMaterials.Count) return;

        // Смена цвета с красного на бирюзовый
        SetSinglePipeStatus(pipeIndex, PipeStatus.Normal);

        if (pipeIndex == randomBrokenIndex)
        {
            randomBrokenIndex = -1;
            Debug.Log($"Труба #{pipeIndex} успешно починена!");
        }
    }

    public void SetSinglePipeStatus(int pipeIndex, PipeStatus status)
    {
        if (pipeIndex < 0 || pipeIndex >= instantiatedMaterials.Count) return;

        Material mat = instantiatedMaterials[pipeIndex];
        if (mat == null) return;

        Color targetColor = status switch
        {
            PipeStatus.Normal => normalColor,
            PipeStatus.Warning => warningColor,
            PipeStatus.Broken => brokenColor,
            _ => normalColor
        };

        mat.EnableKeyword("_EMISSION");
        mat.SetColor(EmissionColorID, targetColor);
    }

    private void OnDestroy()
    {
        foreach (Material mat in instantiatedMaterials)
        {
            if (mat != null) Destroy(mat);
        }
    }
}