using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PipesColorController : MonoBehaviour
{
    public static PipesColorController Instance { get; private set; }

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

    // Текущий вычисленный фоновый цвет каждой трубы (даже когда свечение выключено)
    private Dictionary<int, Color> currentPipeColors = new Dictionary<int, Color>();

    // Отслеживание активных корутин
    private Dictionary<int, Coroutine> activeColorRoutines = new Dictionary<int, Coroutine>();

    private bool isSystemActive = false;
    private bool isMinigameActive = false;
    private int currentInteractingPipeIndex = -1;

    private Coroutine breakdownRoutine;

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
            }
        }
    }

    private void Start()
    {
        // При старте игры случайно делаем одну трубу КРАСНОЙ
        if (instantiatedMaterials.Count > 0)
        {
            int startBrokenIndex = Random.Range(0, instantiatedMaterials.Count);
            pipeStatuses[startBrokenIndex] = PipeStatus.Broken;
            currentPipeColors[startBrokenIndex] = brokenColor;
        }
    }

    private void Update()
    {
        if (!isSystemActive || isMinigameActive) return;

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryRepairPipeUnderCursor();
        }
    }

    /// <summary>
    /// Переключение режима видимости подсветок труб
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
                // При включении инструмента применяем актуальный сгенерированный цвет из памяти
                mat.SetColor(EmissionColorID, currentPipeColors[i]);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor(EmissionColorID, Color.black);
            }
        }
    }

    #region Логика смены состояний и Фоновых Корутин

    public void RepairPipe(int pipeIndex)
    {
        if (!pipeStatuses.ContainsKey(pipeIndex)) return;

        pipeStatuses[pipeIndex] = PipeStatus.Normal;
        
        // Быстро возвращаем починенную трубу в нормальный цвет за 1 секунду
        AnimateColorChange(pipeIndex, normalColor, 1.0f);
        Debug.Log($"Труба #{pipeIndex} починена и восстанавливается!");

        // Запускаем цикл поломки следующей случайной трубы
        if (breakdownRoutine != null) StopCoroutine(breakdownRoutine);
        breakdownRoutine = StartCoroutine(ScheduleNextBreakdownRoutine());
    }

    private IEnumerator ScheduleNextBreakdownRoutine()
    {
        List<int> normalPipes = GetPipesByStatus(PipeStatus.Normal);
        if (normalPipes.Count == 0) yield break;

        int targetPipeIndex = normalPipes[Random.Range(0, normalPipes.Count)];

        // 1. Медленно желтеет в течение 4-10 секунд
        float timeToWarning = Random.Range(4f, 10f);
        Debug.Log($"Труба #{targetPipeIndex} начинает медленно желтеть в фоне ({timeToWarning:F1} сек)...");
        
        yield return AnimateColorChange(targetPipeIndex, warningColor, timeToWarning);
        pipeStatuses[targetPipeIndex] = PipeStatus.Warning;

        // 2. Окно в 10 секунд (труба жёлтая)
        yield return new WaitForSeconds(10f);

        // 3. Если за 10 секунд не починили, медленно краснеет в течение 6-14 секунд
        if (pipeStatuses[targetPipeIndex] == PipeStatus.Warning)
        {
            float timeToBroken = Random.Range(6f, 14f);
            Debug.Log($"Труба #{targetPipeIndex} начинает медленно краснеть в фоне ({timeToBroken:F1} сек)...");

            yield return AnimateColorChange(targetPipeIndex, brokenColor, timeToBroken);
            
            if (pipeStatuses[targetPipeIndex] == PipeStatus.Warning)
            {
                pipeStatuses[targetPipeIndex] = PipeStatus.Broken;
                Debug.Log($"Труба #{targetPipeIndex} стала полностью красной!");
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
            
            // Запоминаем текущий цвет в памяти ВСЕГДА
            currentPipeColors[pipeIndex] = lerpedColor;

            // Назначаем его материалу, ТОЛЬКО если инструмент сейчас активен
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

                    if (currentStatus == PipeStatus.Warning || currentStatus == PipeStatus.Broken)
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