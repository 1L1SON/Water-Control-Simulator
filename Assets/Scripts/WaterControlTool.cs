using UnityEngine;
using UnityEngine.UI;

public class WaterControlTool : MonoBehaviour
{
    [Header("Controllers")]
    [SerializeField] private CityMapController cityMapController;
    [SerializeField] private PipesColorController pipesColorController;

    [SerializeField] private Button button;
    private bool isToggled = false;

    private void Awake()
    {
        button.onClick.AddListener(ToggleShaderProperties);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(ToggleShaderProperties);
        }
    }

    public void ToggleShaderProperties()
    {
        Debug.Log("On Click");
        isToggled = !isToggled;

        if (cityMapController != null)
        {
            cityMapController.SetDarknessAndAlpha(isToggled);
        }

        if (pipesColorController != null)
        {
            pipesColorController.SetPipesColor(isToggled);
        }
    }
}
