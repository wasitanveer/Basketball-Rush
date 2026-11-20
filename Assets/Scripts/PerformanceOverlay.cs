using UnityEngine;
using UnityEngine.InputSystem;

public class PerformanceOverlay : MonoBehaviour
{
    private const float RefreshInterval = 0.25f;

    private static PerformanceOverlay instance;

    private bool visible;
    private bool tripleHeld;

    private float refreshTimer;
    private float frameAccumulator;
    private int frameCount;
    private float worstFrameMs;
    private float displayedFps;
    private float displayedWorstMs;

    private GUIStyle style;
    private string report = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Debug.isDebugBuild && !Application.isEditor)
        {
            return;
        }

        if (FindAnyObjectByType<PerformanceOverlay>() != null)
        {
            return;
        }

        GameObject overlayObject = new GameObject("PerformanceOverlay");
        overlayObject.AddComponent<PerformanceOverlay>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
        {
            visible = !visible;
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            int pressedTouches = 0;

            for (int i = 0; i < touchscreen.touches.Count; i++)
            {
                if (touchscreen.touches[i].press.isPressed)
                {
                    pressedTouches++;
                }
            }

            if (pressedTouches >= 3 && !tripleHeld)
            {
                tripleHeld = true;
                visible = !visible;
            }
            else if (pressedTouches < 3)
            {
                tripleHeld = false;
            }
        }

        if (!visible)
        {
            return;
        }

        float deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        frameAccumulator += deltaTime;
        worstFrameMs = Mathf.Max(worstFrameMs, deltaTime * 1000f);
        frameCount++;
        refreshTimer += deltaTime;

        if (refreshTimer >= RefreshInterval)
        {
            displayedFps = frameCount / frameAccumulator;
            displayedWorstMs = worstFrameMs;
            frameAccumulator = 0f;
            frameCount = 0;
            worstFrameMs = 0f;
            refreshTimer = 0f;
            BuildReport();
        }
    }

    private void BuildReport()
    {
        float battery = SystemInfo.batteryLevel;
        string batteryText = battery < 0f ? "unknown" : Mathf.RoundToInt(battery * 100f) + "%";

        report =
            "FPS: " + displayedFps.ToString("0") +
            "  (" + (1000f / Mathf.Max(1f, displayedFps)).ToString("0.0") + " ms, worst " + displayedWorstMs.ToString("0.0") + " ms)\n" +
            "Screen: " + Screen.width + "x" + Screen.height + "  aspect " + Screen.width / (float)Mathf.Max(1, Screen.height) +
            "  DPI " + Screen.dpi.ToString("0") + "\n" +
            "Target FPS: " + Application.targetFrameRate + "  vSync: " + QualitySettings.vSyncCount +
            "  timeScale: " + Time.timeScale.ToString("0.00") + "\n" +
            "Memory: " + SystemInfo.systemMemorySize + " MB system  " + SystemInfo.graphicsMemorySize + " MB GPU\n" +
            "CPU: " + SystemInfo.processorCount + " cores  GPU: " + SystemInfo.graphicsDeviceName + "\n" +
            "Battery: " + batteryText + "  " + SystemInfo.batteryStatus +
            "  OS: " + SystemInfo.operatingSystem;
    }

    private void OnGUI()
    {
        if (!visible)
        {
            return;
        }

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(14, Screen.height / 54),
                alignment = TextAnchor.UpperLeft,
                wordWrap = false
            };

            style.normal.textColor = Color.white;
        }

        Rect box = new Rect(12f, 12f, Mathf.Min(Screen.width - 24f, 900f), Mathf.Max(150f, Screen.height * 0.24f));
        GUI.color = new Color(0f, 0f, 0f, 0.72f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = Color.white;

        Rect textRect = new Rect(box.x + 12f, box.y + 8f, box.width - 24f, box.height - 16f);
        GUI.Label(textRect, report, style);
    }
}
