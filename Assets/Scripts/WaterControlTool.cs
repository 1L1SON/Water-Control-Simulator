using UnityEngine;
using UnityEngine.UI;


public class WaterControlTool : MonoBehaviour
{
    [Header("Target Materials")] [Tooltip("Перетащите сюда материалы с шейдером CityShaderLit")] [SerializeField]
    private Material[] targetMaterials;

    // Идентификаторы свойств шейдера
    private static readonly int DarknessID = Shader.PropertyToID("_Darkness");
    private static readonly int AlphaID = Shader.PropertyToID("_Alpha");

    private bool isToggled = false;
    [SerializeField]
    private Button button;

    private void Awake()
    {
        // Автоматически подписываем метод на клик кнопки, на которой висит скрипт
        button.onClick.AddListener(ToggleShaderProperties);
    }

    private void OnDestroy()
    {
        // Отписываемся при уничтожении объекта во избежание утечек памяти
        if (button != null)
        {
            button.onClick.RemoveListener(ToggleShaderProperties);
        }
    }

    public void ToggleShaderProperties()
    {
        isToggled = !isToggled;

        // Определяем целевые значения
        float targetDarkness = isToggled ? 0.0f : 1.0f;
        float targetAlpha = isToggled ? 0.6f : 1.0f;

        // Применяем значения к материалам
        foreach (Material mat in targetMaterials)
        {
            if (mat != null)
            {
                mat.SetFloat(DarknessID, targetDarkness);
                mat.SetFloat(AlphaID, targetAlpha);
            }
        }
    }
}