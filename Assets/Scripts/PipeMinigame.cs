using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PipeMinigame : MonoBehaviour 
{
    [Header("UI Elements")]
    [Tooltip("RectTransform движущейся трубы")]
    [SerializeField] private RectTransform movingPipe;
    [Header("Movement Settings")]
    [Tooltip("Левая граница по оси X (local position)")]
    [SerializeField] private float minX = -300f;
    [Tooltip("Правая граница по оси X (local position)")]
    [SerializeField] private float maxX = 300f;
    [Tooltip("Скорость перемещения трубы")]
    [SerializeField] private float moveSpeed = 400f;
    [Header("Target & Tolerance Settings")]
    [Tooltip("Целевая координата X, куда нужно установить трубу")]
    [SerializeField] private float targetPositionX = 0f;
    [Tooltip("Максимально допустимое отклонение по X для зачёта попадания (зона успеха)")]
    [SerializeField] private float toleranceRange = 50f;
    [Tooltip("Процент от toleranceRange для примагничивания (0.5 = 50% от зоны успеха)")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float snapPercentage = 0.5f;
    [Header("Events")]
    public UnityEvent OnMinigameSuccess;
    public UnityEvent OnMinigameFailed;
    private bool isPlaying = true;
    private float currentPingPongTime = 0f;z
    // snapRange вычисляется автоматически
    private float CalculatedSnapRange => toleranceRange * snapPercentage;

    private void Awake()
    {
        ResetMinigame();
    }
    
    private void Update()
    {
        if (!isPlaying || movingPipe == null) return;

        // Движение трубы влево-вправо по X
        currentPingPongTime += Time.deltaTime * (moveSpeed / Mathf.Abs(maxX - minX));
        float newX = Mathf.Lerp(minX, maxX, Mathf.PingPong(currentPingPongTime, 1f));

        Vector2 anchoredPos = movingPipe.anchoredPosition;
        anchoredPos.x = newX;
        movingPipe.anchoredPosition = anchoredPos;

        // Нажатие на Пробел (New Input System)
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            StopAndCheckTiming();
        }
    }

    private void StopAndCheckTiming()
    {
        isPlaying = false;

        float currentX = movingPipe.anchoredPosition.x;
        float distanceToTarget = Mathf.Abs(currentX - targetPositionX);

        // Автоматически вычисленный snapRange
        float currentSnapRange = CalculatedSnapRange;

        // Автокоррекция (Snap): примагничиваем, если расстояние меньше вычисленного snapRange
        if (distanceToTarget <= currentSnapRange)
        {
            Vector2 anchoredPos = movingPipe.anchoredPosition;
            anchoredPos.x = targetPositionX;
            movingPipe.anchoredPosition = anchoredPos;
            
            distanceToTarget = 0f; // Принудительно делаем идеальное попадание
        }

        // Проверка попадания в общую зону успеха
        if (distanceToTarget <= toleranceRange)
        {
            Debug.Log($"<color=green>Успех! Дистанция: {distanceToTarget} (Snap был: {currentSnapRange})</color>");
            OnMinigameSuccess?.Invoke();
            PipesColorController.Instance.OnMinigameCompleted(true);
        }
        else
        {
            Debug.Log("<color=red>Промах!</color>");
            OnMinigameFailed?.Invoke();
            PipesColorController.Instance.OnMinigameCompleted(false);
        }
    }

    public void ResetMinigame()
    {
        isPlaying = true;
        currentPingPongTime = 0f;
        moveSpeed = PipesColorController.Instance.MiniGameSpeed;
    }
}