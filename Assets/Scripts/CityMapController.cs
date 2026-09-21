using UnityEngine;

public class CityMapController : MonoBehaviour
{
    [Header("Target Materials")]
    [Tooltip("Материалы зданий/карты с шейдером CityShaderLit")]
    [SerializeField] private Material[] cityMaterials;

    private static readonly int DarknessID = Shader.PropertyToID("_Darkness");
    private static readonly int AlphaID = Shader.PropertyToID("_Alpha");

    public void SetDarknessAndAlpha(bool isToggled)
    {
        float targetDarkness = isToggled ? 0.0f : 1.0f;
        float targetAlpha = isToggled ? 0.6f : 1.0f;

        foreach (Material mat in cityMaterials)
        {
            if (mat != null)
            {
                mat.SetFloat(DarknessID, targetDarkness);
                mat.SetFloat(AlphaID, targetAlpha);
            }
        }
    }
}