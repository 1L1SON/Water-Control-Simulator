using UnityEngine;
using UnityEngine.InputSystem;

public class FreeCamNewInputSystem : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 10f;          // Скорость перемещения
    public float mouseSensitivity = 2f;    // Чувствительность мыши

    private InputActionMap actionMap;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction enableLookAction;

    private float rotationX = 0f;  // Yaw
    private float rotationY = 0f;  // Pitch

    private void Awake()
    {
        // Блокируем курсор в центре экрана и скрываем его
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Создаём Input Action Map
        actionMap = new InputActionMap("FreeCam");

        // Перемещение (WASD + стрелки)
        moveAction = actionMap.AddAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");

        // Взгляд (движение мыши)
        lookAction = actionMap.AddAction("Look", InputActionType.Value);
        lookAction.AddBinding("<Mouse>/delta");

        // Кнопка включения вращения (правая кнопка мыши)
        enableLookAction = actionMap.AddAction("EnableLook", InputActionType.Button);
        enableLookAction.AddBinding("<Mouse>/rightButton");

        actionMap.Enable();

        // Инициализируем углы из текущего поворота камеры
        Vector3 euler = transform.eulerAngles;
        rotationX = euler.y;
        rotationY = euler.x;
        if (rotationY > 180f) rotationY -= 360f;
    }

    private void Update()
    {
        // Обработка выхода из режима свободной камеры (Esc)
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Повторный захват курсора по клику левой кнопкой мыши
        if (Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Вращение камеры только при заблокированном курсоре и зажатой ПКМ
        if (Cursor.lockState == CursorLockMode.Locked && enableLookAction.ReadValue<float>() > 0.5f)
        {
            Vector2 lookDelta = lookAction.ReadValue<Vector2>();
            rotationX += lookDelta.x * mouseSensitivity;
            rotationY -= lookDelta.y * mouseSensitivity;
            rotationY = Mathf.Clamp(rotationY, -90f, 90f);
        }

        // Применяем поворот
        transform.localRotation = Quaternion.Euler(rotationY, rotationX, 0f);

        // Перемещение полностью в направлении взгляда (включая вертикаль)
        Vector2 moveInput = moveAction.ReadValue<Vector2>();
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

        if (move.sqrMagnitude > 0.01f)
        {
            move.Normalize();
            transform.position += move * moveSpeed * Time.deltaTime;
        }
    }

    private void OnDestroy()
    {
        actionMap?.Dispose();
    }
}