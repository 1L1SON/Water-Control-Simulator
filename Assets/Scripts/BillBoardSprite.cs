using UnityEngine;

public class BillboardSprite : MonoBehaviour
{
    [Header("Camera Reference")]
    [Tooltip("Ссылка на главную камеру. Если не назначена, найдется автоматически.")]
    [SerializeField] private Camera targetCamera;

    [Header("Scale Settings")]
    [Tooltip("Включить динамическое изменение масштаба в зависимости от расстояния.")]
    [SerializeField] private bool autoScaleWithDistance = true;

    [Tooltip("Базовый масштаб объекта (размер на стандартном расстоянии).")]
    [SerializeField] private Vector3 baseScale = Vector3.one;

    [Tooltip("Множитель масштаба относительно расстояния.")]
    [SerializeField] private float scaleFactor = 0.1f;

    private void Awake()
    {
        // Если камера не перетащена в инспектор, берем MainCamera
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        // Сохраняем стартовый масштаб объекта, если он не был задан вручную
        if (baseScale == Vector3.one && transform.localScale != Vector3.one)
        {
            baseScale = transform.localScale;
        }
    }

    // LateUpdate используется, чтобы сработало ПОСЛЕ того, как камера завершит свое движение в Update
    private void LateUpdate()
    {
        if (targetCamera == null) return;

        // 1. ПОВОРОТ К КАМЕРЕ
        // Поворачиваем объект параллельно плоскости камеры (классический билбординг)
        transform.rotation = targetCamera.transform.rotation;

        // 2. ИЗМЕНЕНИЕ МАСШТАБА ОТ РАССТОЯНИЯ
        if (autoScaleWithDistance)
        {
            // Вычисляем расстояние от объекта до камеры
            float distance = Vector3.Distance(transform.position, targetCamera.transform.position);

            // Чем дальше объект, тем больше коэффициент
            transform.localScale = baseScale * (distance * scaleFactor);
        }
    }
}