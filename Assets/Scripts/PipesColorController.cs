using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public class PipesColorController : MonoBehaviour
{
    public static PipesColorController Instance { get; private set; }

    // События для победы и поражения
    public static event Action OnGameWon;
    public static event Action OnGameLost;

    public enum PipeStatus
    {
        Normal,     // Бирюзовый
        Warning,    // Жёлтый
        Broken      // Красный
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
    private Dictionary<int, PipeStatus> pipeStatuses = new Dictionary<int, PipeStatus>();

    private Dictionary<int, Color> currentPipeColors = new Dictionary<int, Color>();
    private Dictionary<int, Coroutine> activeColorRoutines = new Dictionary<int, Coroutine>();

    private bool isSystemActive = false;
    private bool isMinigameActive = false;
    private bool isGameOver = false;
    private int currentInteractingPipeIndex = -1;

    private Coroutine breakdownRoutine;
    public float MiniGameSpeed;

    private void Awake()
    {
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

        for (int i = 0; i < pipeRenderers.Length; i++)
        {
            MeshRenderer rend = pipeRenderers[i];
            if (rend != null)
            {
                Material instanceMat = rend.material;
                instantiatedMaterials.Add(instanceMat);
                rendererToIndexMap[rend] = i;

                pipeStatuses[i] = PipeStatus.Normal;
                currentPipeColors[i] = normalColor;

                instanceMat.DisableKeyword("_EMISSION");
                instanceMat.SetColor(EmissionColorID, Color.black);
                rend.enabled = false;
            }
        }
    }

    private void Start()
    {
        int totalPipes = instantiatedMaterials.Count;

        if (totalPipes > 0)
        {
            // 1. Создаём список индексов всех труб [0, 1, 2, ..., N-1]
            List<int> pipeIndices = new List<int>();
            for (int i = 0; i < totalPipes; i++)
            {
                pipeIndices.Add(i);
            }

            // 2. Рассчитываем половину труб (округление вверх, чтобы при нечётном количестве было чуть больше)
            int affectedPipesCount = Mathf.CeilToInt(totalPipes / 2f);

            // 3. Выбираем случайные трубы и распределяем их между Broken и Warning
            for (int i = 0; i < affectedPipesCount; i++)
            {
                // Выбираем случайный индекс из оставшихся
                int randomIndexPosition = Random.Range(0, pipeIndices.Count);
                int selectedPipeIndex = pipeIndices[randomIndexPosition];

                // Удаляем, чтобы одна и та же труба не выбралась дважды
                pipeIndices.RemoveAt(randomIndexPosition);

                // 50% шанс сделать трубу Красной (Broken) или Жёлтой (Warning)
                bool makeBroken = Random.value > 0.5f;

                if (makeBroken)
                {
                    pipeStatuses[selectedPipeIndex] = PipeStatus.Broken;
                    currentPipeColors[selectedPipeIndex] = brokenColor;
                }
                else
                {
                    pipeStatuses[selectedPipeIndex] = PipeStatus.Warning;
                    currentPipeColors[selectedPipeIndex] = warningColor;
                }
            }

            // Запускаем фоновый цикл поломок для оставшихся целых труб
            RestartBreakdownRoutine();
        }

        CheckGameConditions();
    }

    private void Update()
    {
        if (isGameOver || !isSystemActive || isMinigameActive) return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryRepairPipeUnderCursor();
        }
    }

    public void SetPipesColor(bool isToggled)
    {
        isSystemActive = isToggled;
        if (pipeRenderers == null || pipeRenderers.Length == 0) return;

        for (int i = 0; i < pipeRenderers.Length; i++)
        {
            MeshRenderer rend = pipeRenderers[i];
            Material mat = instantiatedMaterials[i];
            
            if (rend == null || mat == null) continue;

            if (isToggled)
            {
                rend.enabled = true;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor(EmissionColorID, currentPipeColors[i]);
            }
            else
            {
                rend.enabled = false;
                mat.DisableKeyword("_EMISSION");
                mat.SetColor(EmissionColorID, Color.black);
            }
        }
    }

    #region Логика смены состояний и Фоновых Корутин

    public void RepairPipe(int pipeIndex)
    {
        if (isGameOver || !pipeStatuses.ContainsKey(pipeIndex)) return;

        pipeStatuses[pipeIndex] = PipeStatus.Normal;
        AnimateColorChange(pipeIndex, normalColor, 1.0f);
        Debug.Log($"Труба #{pipeIndex} починена!");

        CheckGameConditions();

        if (!isGameOver)
            RestartBreakdownRoutine();
    }

    public void BreakRandomPipeImmediately()
    {
        if (isGameOver) return;

        List<int> availablePipes = GetPipesByStatus(PipeStatus.Normal);
        availablePipes.AddRange(GetPipesByStatus(PipeStatus.Warning));

        if (availablePipes.Count == 0)
        {
            CheckGameConditions();
            return;
        }

        int targetIndex = availablePipes[Random.Range(0, availablePipes.Count)];
        pipeStatuses[targetIndex] = PipeStatus.Broken;
        AnimateColorChange(targetIndex, brokenColor, 0.2f);
        Debug.Log($"Мини-игра провалена! Труба #{targetIndex} моментально сломалась!");

        CheckGameConditions();

        if (!isGameOver)
            RestartBreakdownRoutine();
    }

    private void RestartBreakdownRoutine()
    {
        if (breakdownRoutine != null) StopCoroutine(breakdownRoutine);
        breakdownRoutine = StartCoroutine(ScheduleNextBreakdownRoutine());
    }

    private IEnumerator ScheduleNextBreakdownRoutine()
    {
        // Пауза 5-8 секунд после починки (или при запуске) перед началом поломки следующей трубы
        float delayBeforeBreakdown = Random.Range(5f, 8f);
        Debug.Log($"Ожидание {delayBeforeBreakdown:F1} сек. перед началом поломки следующей трубы...");
        yield return new WaitForSeconds(delayBeforeBreakdown);

        List<int> normalPipes = GetPipesByStatus(PipeStatus.Normal);
        if (normalPipes.Count == 0 || isGameOver) yield break;

        int targetPipeIndex = normalPipes[Random.Range(0, normalPipes.Count)];
        float timeToWarning = Random.Range(4f, 10f);
    
        Debug.Log($"Труба #{targetPipeIndex} начинает медленно желтеть ({timeToWarning:F1} сек)...");
        yield return AnimateColorChange(targetPipeIndex, warningColor, timeToWarning);
        if (isGameOver) yield break;

        pipeStatuses[targetPipeIndex] = PipeStatus.Warning;

        yield return new WaitForSeconds(10f);
        if (isGameOver) yield break;

        if (pipeStatuses[targetPipeIndex] == PipeStatus.Warning)
        {
            float timeToBroken = Random.Range(6f, 14f);
            Debug.Log($"Труба #{targetPipeIndex} начинает медленно краснеть ({timeToBroken:F1} сек)...");
            yield return AnimateColorChange(targetPipeIndex, brokenColor, timeToBroken);
        
            if (pipeStatuses[targetPipeIndex] == PipeStatus.Warning)
            {
                pipeStatuses[targetPipeIndex] = PipeStatus.Broken;
                Debug.Log($"Труба #{targetPipeIndex} стала полностью красной!");
                CheckGameConditions();
            }
        }
    }

    private Coroutine AnimateColorChange(int pipeIndex, Color targetColor, float duration)
    {
        if (pipeIndex < 0 || pipeIndex >= instantiatedMaterials.Count) return null;

        if (activeColorRoutines.ContainsKey(pipeIndex) && activeColorRoutines[pipeIndex] != null)
        {
            StopCoroutine(activeColorRoutines[pipeIndex]);
        }

        Coroutine routine = StartCoroutine(ContinuousColorChangeRoutine(pipeIndex, targetColor, duration));
        activeColorRoutines[pipeIndex] = routine;
        return routine;
    }

    private IEnumerator ContinuousColorChangeRoutine(int pipeIndex, Color targetColor, float duration)
    {
        Material mat = instantiatedMaterials[pipeIndex];
        Color startColor = currentPipeColors[pipeIndex];
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            Color lerpedColor = Color.Lerp(startColor, targetColor, elapsedTime / duration);
            currentPipeColors[pipeIndex] = lerpedColor;

            if (isSystemActive && mat != null)
            {
                mat.SetColor(EmissionColorID, lerpedColor);
            }

            yield return null;
        }

        currentPipeColors[pipeIndex] = targetColor;

        if (isSystemActive && mat != null)
        {
            mat.SetColor(EmissionColorID, targetColor);
        }
    }

    private List<int> GetPipesByStatus(PipeStatus status)
    {
        List<int> result = new List<int>();
        foreach (var pair in pipeStatuses)
        {
            if (pair.Value == status)
                result.Add(pair.Key);
        }
        return result;
    }

    /// <summary>
    /// Проверка условий Победы (все трубы Normal) или Поражения (все трубы Broken)
    /// </summary>
    private void CheckGameConditions()
    {
        if (isGameOver || pipeStatuses.Count == 0) return;

        int brokenCount = GetPipesByStatus(PipeStatus.Broken).Count;
        int normalCount = GetPipesByStatus(PipeStatus.Normal).Count;
        int totalPipes = pipeStatuses.Count;

        // Поражение: ВСЕ трубы сломаны (красные)
        if (brokenCount >= totalPipes)
        {
            isGameOver = true;
            if (breakdownRoutine != null) StopCoroutine(breakdownRoutine);
            OnGameLost?.Invoke();
            Debug.Log("<color=red>ПОРАЖЕНИЕ: Все трубы сломались!</color>");
        }
        // Победа: ВСЕ трубы восстановлены (зелёные/бирюзовые)
        else if (normalCount >= totalPipes)
        {
            isGameOver = true;
            if (breakdownRoutine != null) StopCoroutine(breakdownRoutine);
            OnGameWon?.Invoke();
            Debug.Log("<color=green>ПОБЕДА: Все трубы исправны!</color>");
        }
    }

    #endregion

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
                    PipeStatus currentStatus = pipeStatuses[pipeIndex];

                    if (currentStatus == PipeStatus.Warning)
                    {
                        currentInteractingPipeIndex = pipeIndex;
                        StartMinigame();
                        MiniGameSpeed = 400f;
                    }
                    else if (currentStatus == PipeStatus.Broken)
                    {
                        currentInteractingPipeIndex = pipeIndex;
                        StartMinigame();
                        MiniGameSpeed = 800f;
                    }
                }
            }
        }
    }

    private void StartMinigame()
    {
        isMinigameActive = true;
        SceneManager.LoadScene(minigameSceneName, LoadSceneMode.Additive);
    }

    public void OnMinigameCompleted(bool success)
    {
        SceneManager.UnloadSceneAsync(minigameSceneName);

        if (success && currentInteractingPipeIndex != -1)
        {
            RepairPipe(currentInteractingPipeIndex);
        }
        else if (!success)
        {
            BreakRandomPipeImmediately();
        }

        currentInteractingPipeIndex = -1;
        isMinigameActive = false;
    }

    private void OnDestroy()
    {
        foreach (Material mat in instantiatedMaterials)
        {
            if (mat != null) Destroy(mat);
        }
    }
}