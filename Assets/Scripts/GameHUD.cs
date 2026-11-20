using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameHUD : MonoBehaviour
{
    private const string CanvasObjectName = "BasketballHUDCanvas";

    private static GameHUD instance;
    private static GameObject createdCanvas;

    [Header("References")]
    [Tooltip("The existing scoring system. If empty, it is found automatically.")]
    public HoopScoreSystem hoopScore;

    [Tooltip("The ball controller used to block throws after time runs out. If empty, it is found automatically.")]
    public BallThrowController ballThrowController;

    [Header("Timer")]
    [Tooltip("Seconds shown at the start of the game.")]
    public int startingTime = 60;

    [Header("Appearance")]
    [Tooltip("Optional font override. If empty, the bundled HUD font is used.")]
    public Font hudFont;

    public Color textColor = Color.white;
    public int fontSize = 34;
    public int timeUpFontSize = 72;

    [Header("Audio")]
    [Tooltip("Basket score sound. Loaded from Resources/Audio/Score if empty.")]
    public AudioClip scoreClip;

    [Tooltip("Time-up sound. Loaded from Resources/Audio/TimeUp if empty.")]
    public AudioClip timeUpClip;

    [Range(0f, 1f)] public float gameAudioVolume = 0.85f;

    private static Font sharedFont;

    private Text scoreText;
    private Text timerText;
    private Text timeUpText;
    private AudioSource gameAudioSource;
    private float timeRemaining;
    private int displayedTime;
    private int lastScore;
    private bool timerFinished;
    private int pendingTextRefreshes = 2;

    private Text scorePopText;
    private RectTransform scoreRect;
    private Vector3 scoreBaseScale = Vector3.one;
    private float scorePulseTimer;
    private float scorePopTimer;

    private const float ScorePulseDuration = 0.18f;
    private const float ScorePulseScale = 1.12f;
    private const float ScorePopDuration = 0.9f;
    private const float ScorePopFadeDuration = 0.3f;
    private static readonly Color ScorePopColor = new Color(1f, 0.82f, 0.25f, 1f);

    private const float HudPanelWidth = 248f;
    private const float HudPanelHeight = 76f;
    private const float HudPanelMarginX = 40f;
    private const float HudPanelMarginY = 26f;
    private const float HudIconSize = 42f;
    private const float HudIconLeftPadding = 15f;
    private const float HudIconTextGap = 10f;
    private const float HudTextRightPadding = 16f;
    private const float PowerPanelWidth = 420f;
    private const float PowerPanelHeight = 80f;
    private const float PowerPanelMarginBottom = 32f;
    private const float PowerBarWidth = 360f;
    private const float PowerBarHeight = 16f;
    private const float PowerBarFillHeight = 10f;
    private const float PowerBarInset = 3f;
    private const int PowerLabelFontSize = 22;
    private const int PowerPercentFontSize = 18;
    private static readonly Color HudPanelColor = new Color(0.055f, 0.06f, 0.07f, 0.86f);
    private static readonly Color HudPanelBorderColor = new Color(1f, 1f, 1f, 0.16f);
    private static readonly Color HudPanelShadowColor = new Color(0f, 0f, 0f, 0.4f);
    private static readonly Color PowerTrackColor = new Color(0.015f, 0.018f, 0.022f, 0.92f);
    private static readonly Color PowerFillColor = new Color(1f, 0.52f, 0.1f, 1f);
    private static readonly Color PowerAccentColor = new Color(1f, 0.62f, 0.23f, 1f);

    private static Sprite hudPanelFillSprite;
    private static Sprite hudPanelBorderSprite;
    private static Sprite basketballIconSprite;
    private static Sprite timerIconSprite;
    private static Sprite powerBarTrackSprite;
    private static Sprite powerBarBorderSprite;
    private static Sprite powerBarFillSprite;

    private GameObject scorePanelObject;
    private GameObject timerPanelObject;
    private GameObject powerPanelObject;
    private RectTransform powerBarFillRect;
    private Image powerBarFillImage;
    private Text powerPercentText;
    private float powerBarFillMaxWidth;
    private int displayedPowerPercent = -1;

    private GameObject startPanel;
    private Image startButtonImage;
    private RectTransform startButtonRect;
    private Text startTitleText;
    private Text startSubtitleText;
    private Text startButtonText;
    private GameObject gameOverPanel;
    private Text gameOverScoreText;
    private Text gameOverHintText;
    private bool gameStarted;

    private static readonly Color StartButtonNormalColor = new Color(1f, 0.45f, 0.12f, 1f);
    private static readonly Color StartButtonHoverColor = new Color(1f, 0.57f, 0.22f, 1f);
    private static readonly Color StartButtonPressedColor = new Color(0.86f, 0.33f, 0.06f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadHandler()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureHudExists();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureHudExists()
    {
        if (instance != null)
        {
            return;
        }

        if (GameObject.Find(CanvasObjectName) != null)
        {
            return;
        }

        if (FindObjectsByType<GameHUD>(FindObjectsInactive.Include).Length > 0)
        {
            return;
        }

        GameObject hudObject = new GameObject("GameHUD");
        hudObject.AddComponent<GameHUD>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            enabled = false;
            Destroy(this);
            return;
        }

        instance = this;

        if (hoopScore == null)
        {
            hoopScore = FindAnyObjectByType<HoopScoreSystem>();
        }

        if (ballThrowController == null)
        {
            ballThrowController = FindAnyObjectByType<BallThrowController>();
        }

        SetupAudio();
        BuildHud();
    }

    private void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        instance = null;

        if (createdCanvas != null)
        {
            Destroy(createdCanvas);
            createdCanvas = null;
        }
    }

    private void Start()
    {
        gameStarted = false;

        if (hoopScore != null)
        {
            hoopScore.ResetScore();
        }

        lastScore = 0;
        timeRemaining = Mathf.Max(0f, startingTime);
        displayedTime = Mathf.CeilToInt(timeRemaining);
        timerFinished = false;

        if (scoreText != null)
        {
            scoreText.text = "Score: 0";
            scoreText.gameObject.SetActive(false);
            scoreText.SetAllDirty();
        }

        if (timerText != null)
        {
            timerText.text = "Time: " + displayedTime;
            timerText.gameObject.SetActive(false);
            timerText.SetAllDirty();
        }

        SetHudPanelsVisible(false);
        SetPowerPanelVisible(false);
        ResetPowerMeter();

        if (timeUpText != null) timeUpText.gameObject.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (gameOverScoreText != null) gameOverScoreText.gameObject.SetActive(false);
        if (gameOverHintText != null) gameOverHintText.gameObject.SetActive(false);
        if (startPanel != null) startPanel.SetActive(true);

        ResetScoreFeedback();

        if (ballThrowController != null)
        {
            ballThrowController.RestartBall();
            ballThrowController.canThrow = false;
        }

        Canvas.ForceUpdateCanvases();
    }

    private void Update()
    {
        UpdateScoreText();
        UpdateTimer();
        UpdateScoreFeedback();
        UpdatePowerMeter();
        HandleStartInput();
        HandleRestartInput();
    }

    private void HandleStartInput()
    {
        if (startPanel == null || !startPanel.activeSelf || startButtonRect == null)
        {
            return;
        }

        if (!TryReadPointer(out Vector2 pointerPosition, out bool pointerHeld, out bool pointerReleased))
        {
            return;
        }

        bool overButton = RectTransformUtility.RectangleContainsScreenPoint(startButtonRect, pointerPosition, null);
        bool pressed = overButton && pointerHeld;

        if (startButtonImage != null)
        {
            startButtonImage.color = pressed
                ? StartButtonPressedColor
                : (overButton ? StartButtonHoverColor : StartButtonNormalColor);
        }

        if (startButtonRect != null)
        {
            startButtonRect.localScale = Vector3.one * (pressed ? 0.97f : (overButton ? 1.03f : 1f));
        }

        if (overButton && pointerReleased)
        {
            StartGame();
        }
    }

    private static bool TryReadPointer(out Vector2 position, out bool held, out bool released)
    {
        position = Vector2.zero;
        held = false;
        released = false;

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            var primaryTouch = touchscreen.primaryTouch;
            if (primaryTouch.press.isPressed || primaryTouch.press.wasReleasedThisFrame)
            {
                position = primaryTouch.position.ReadValue();
                held = primaryTouch.press.isPressed;
                released = primaryTouch.press.wasReleasedThisFrame;
                return true;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            position = mouse.position.ReadValue();
            held = mouse.leftButton.isPressed;
            released = mouse.leftButton.wasReleasedThisFrame;
            return true;
        }

        return false;
    }

    public void StartGame()
    {
        RestartGame();
    }

    private void HandleRestartInput()
    {
        if (!gameStarted)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            RestartGame();
            return;
        }

        if (!timerFinished)
        {
            return;
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.wasReleasedThisFrame)
        {
            RestartGame();
        }
    }

    public void RestartGame()
    {
        gameStarted = true;

        if (gameAudioSource != null)
        {
            gameAudioSource.Stop();
        }

        timeRemaining = Mathf.Max(0f, startingTime);
        displayedTime = Mathf.CeilToInt(timeRemaining);
        timerFinished = false;

        if (startPanel != null) startPanel.SetActive(false);
        if (startButtonRect != null) startButtonRect.localScale = Vector3.one;
        if (startButtonImage != null) startButtonImage.color = StartButtonNormalColor;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (gameOverScoreText != null) gameOverScoreText.gameObject.SetActive(false);
        if (gameOverHintText != null) gameOverHintText.gameObject.SetActive(false);

        if (timerText != null)
        {
            timerText.gameObject.SetActive(true);
            timerText.text = "Time: " + displayedTime;
            timerText.SetAllDirty();
        }

        if (timeUpText != null)
        {
            timeUpText.gameObject.SetActive(false);
        }

        if (hoopScore != null)
        {
            hoopScore.ResetScore();
        }

        lastScore = 0;

        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(true);
            scoreText.text = "Score: 0";
            scoreText.SetAllDirty();
        }

        SetHudPanelsVisible(true);
        SetPowerPanelVisible(true);
        ResetPowerMeter();

        ResetScoreFeedback();

        if (ballThrowController != null)
        {
            ballThrowController.RestartBall();
        }

        Canvas.ForceUpdateCanvases();
    }

    private void LateUpdate()
    {
        if (pendingTextRefreshes <= 0)
        {
            return;
        }

        pendingTextRefreshes--;

        if (scoreText != null)
        {
            scoreText.SetAllDirty();
        }

        if (timerText != null)
        {
            timerText.SetAllDirty();
        }

        if (timeUpText != null && timeUpText.gameObject.activeSelf)
        {
            timeUpText.SetAllDirty();
        }

        if (startTitleText != null) startTitleText.SetAllDirty();
        if (startSubtitleText != null) startSubtitleText.SetAllDirty();
        if (startButtonText != null) startButtonText.SetAllDirty();

        Canvas.ForceUpdateCanvases();
    }

    private void UpdateScoreText()
    {
        if (hoopScore == null || scoreText == null)
        {
            return;
        }

        int currentScore = hoopScore.score;
        if (currentScore != lastScore)
        {
            if (currentScore > lastScore)
            {
                PlayGameSound(scoreClip, 0.9f);
                ShowScoreFeedback();
            }

            lastScore = currentScore;
            scoreText.text = "Score: " + currentScore;

            if (timerFinished && gameOverScoreText != null && gameOverScoreText.gameObject.activeSelf)
            {
                gameOverScoreText.text = "Final Score: " + currentScore;
                gameOverScoreText.SetAllDirty();
            }
        }
    }

    private void UpdateTimer()
    {
        if (!gameStarted || timerFinished)
        {
            return;
        }

        timeRemaining -= Time.deltaTime;

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            timerFinished = true;
            timerText.text = "Time: 0";
            timeUpText.gameObject.SetActive(true);
            ShowGameOver();
            PlayGameSound(timeUpClip, 0.9f);

            if (ballThrowController != null)
            {
                ballThrowController.canThrow = false;
            }

            return;
        }

        int newDisplayedTime = Mathf.CeilToInt(timeRemaining);
        if (newDisplayedTime != displayedTime)
        {
            displayedTime = newDisplayedTime;
            timerText.text = "Time: " + displayedTime;
        }
    }

    private void BuildHud()
    {
        if (createdCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject(
            CanvasObjectName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.layer = 5;
        canvasObject.transform.localScale = Vector3.one;
        createdCanvas = canvasObject;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject safeAreaObject = new GameObject("SafeArea", typeof(RectTransform));
        safeAreaObject.layer = 5;
        safeAreaObject.transform.SetParent(canvasObject.transform, false);

        RectTransform hudRoot = safeAreaObject.GetComponent<RectTransform>();
        hudRoot.anchorMin = Vector2.zero;
        hudRoot.anchorMax = Vector2.one;
        hudRoot.pivot = new Vector2(0.5f, 0.5f);
        hudRoot.offsetMin = Vector2.zero;
        hudRoot.offsetMax = Vector2.zero;
        hudRoot.localScale = Vector3.one;
        hudRoot.localRotation = Quaternion.identity;
        safeAreaObject.AddComponent<SafeAreaFitter>();

        Font font = ResolveFont();

        RectTransform scorePanel = CreateHudPanel(
            hudRoot,
            "ScorePanel",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(HudPanelMarginX, -HudPanelMarginY));
        scorePanelObject = scorePanel.gameObject;
        CreateHudIcon(scorePanel, "ScoreIcon", GetBasketballIconSprite());

        scoreText = CreateText(
            scorePanel,
            "ScoreText",
            font,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(HudIconLeftPadding + HudIconSize + HudIconTextGap, 0f),
            new Vector2(HudPanelWidth - HudIconLeftPadding - HudIconSize - HudIconTextGap - HudTextRightPadding, HudPanelHeight),
            TextAnchor.MiddleLeft,
            fontSize,
            false);
        StyleHudValueText(scoreText);

        RectTransform timerPanel = CreateHudPanel(
            hudRoot,
            "TimerPanel",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-HudPanelMarginX, -HudPanelMarginY));
        timerPanelObject = timerPanel.gameObject;
        CreateHudIcon(timerPanel, "TimerIcon", GetTimerIconSprite());

        timerText = CreateText(
            timerPanel,
            "TimerText",
            font,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(HudIconLeftPadding + HudIconSize + HudIconTextGap, 0f),
            new Vector2(HudPanelWidth - HudIconLeftPadding - HudIconSize - HudIconTextGap - HudTextRightPadding, HudPanelHeight),
            TextAnchor.MiddleLeft,
            fontSize,
            false);
        StyleHudValueText(timerText);

        BuildPowerMeter(hudRoot, font);

        timeUpText = CreateText(
            hudRoot,
            "TimeUpText",
            font,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 85f),
            new Vector2(1000f, 160f),
            TextAnchor.MiddleCenter,
            Mathf.Clamp(timeUpFontSize, 60, 80));

        scoreRect = scoreText.rectTransform;
        scoreBaseScale = scoreRect.localScale;

        scorePopText = CreateText(
            hudRoot,
            "ScorePopText",
            font,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(HudPanelMarginX + HudPanelWidth + 14f, -(HudPanelMarginY + (HudPanelHeight - 60f) * 0.5f)),
            new Vector2(140f, 60f),
            TextAnchor.MiddleLeft,
            fontSize + 4);

        scorePopText.color = ScorePopColor;
        scorePopText.text = "+1";
        scorePopText.gameObject.SetActive(false);

        gameOverScoreText = CreateText(
            hudRoot,
            "GameOverScoreText",
            font,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -15f),
            new Vector2(1000f, 80f),
            TextAnchor.MiddleCenter,
            46);

        gameOverHintText = CreateText(
            hudRoot,
            "GameOverHintText",
            font,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -95f),
            new Vector2(1000f, 60f),
            TextAnchor.MiddleCenter,
            26);

        gameOverScoreText.text = "Final Score: 0";
        gameOverHintText.text = "Press R to play again";
        gameOverHintText.color = new Color(0.76f, 0.78f, 0.82f, 1f);
        gameOverScoreText.gameObject.SetActive(false);
        gameOverHintText.gameObject.SetActive(false);

        gameOverPanel = CreateOverlayPanel(
            hudRoot,
            "GameOverPanel",
            new Color(0.03f, 0.04f, 0.06f, 0.78f),
            true);

        startPanel = CreateOverlayPanel(
            hudRoot,
            "StartPanel",
            new Color(0.03f, 0.04f, 0.06f, 0.82f),
            false);
        BuildStartScreen(startPanel.transform, font);

        gameOverPanel.SetActive(false);

        scoreText.text = "Score: 0";
        timerText.text = "Time: " + Mathf.Max(0, startingTime);
        timeUpText.text = "TIME UP";
        timeUpText.gameObject.SetActive(false);

        Canvas.ForceUpdateCanvases();
    }

    private Font ResolveFont()
    {
        if (sharedFont != null)
        {
            return sharedFont;
        }

        if (hudFont != null)
        {
            sharedFont = hudFont;
            return sharedFont;
        }

        Font font = Resources.Load<Font>("HUDFont");
        if (font == null)
        {
            font = TryGetBuiltinFont("LegacyRuntime.ttf");
        }

        if (font == null)
        {
            font = TryGetBuiltinFont("Arial.ttf");
        }

        if (font == null)
        {
            string[] candidates = { "Segoe UI", "Arial", "Tahoma", "Verdana", "Helvetica" };
            for (int i = 0; i < candidates.Length && font == null; i++)
            {
                try
                {
                    font = Font.CreateDynamicFontFromOSFont(candidates[i], Mathf.Max(16, fontSize));
                }
                catch
                {
                    font = null;
                }
            }
        }

        if (font == null)
        {
            Debug.LogError("[GameHUD] No usable font was found. HUD text cannot be rendered.");
        }

        sharedFont = font;
        return sharedFont;
    }

    private static Font TryGetBuiltinFont(string resourceName)
    {
        try
        {
            return Resources.GetBuiltinResource<Font>(resourceName);
        }
        catch
        {
            return null;
        }
    }

    private Text CreateText(
        Transform parent,
        string objectName,
        Font font,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        TextAnchor alignment,
        int textSize,
        bool addOutline = true)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.layer = 5;
        textObject.SetActive(false);
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;

        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = textSize;
        text.color = new Color(textColor.r, textColor.g, textColor.b, 1f);
        text.alignment = alignment;
        text.alignByGeometry = false;
        text.resizeTextForBestFit = false;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.maskable = false;
        text.enabled = true;

        CanvasRenderer canvasRenderer = text.canvasRenderer;
        canvasRenderer.cull = false;
        canvasRenderer.SetAlpha(1f);

        if (addOutline)
        {
            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
        }

        textObject.SetActive(true);
        text.SetAllDirty();

        return text;
    }

    private void SetHudPanelsVisible(bool visible)
    {
        if (scorePanelObject != null)
        {
            scorePanelObject.SetActive(visible);
        }

        if (timerPanelObject != null)
        {
            timerPanelObject.SetActive(visible);
        }
    }

    private void SetPowerPanelVisible(bool visible)
    {
        if (powerPanelObject != null)
        {
            powerPanelObject.SetActive(visible);
        }
    }

    private void BuildPowerMeter(Transform parent, Font font)
    {
        RectTransform panel = CreateHudPanel(
            parent,
            "PowerPanel",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, PowerPanelMarginBottom),
            new Vector2(PowerPanelWidth, PowerPanelHeight));
        powerPanelObject = panel.gameObject;

        Text powerLabel = CreateText(
            panel,
            "PowerLabel",
            font,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -8f),
            new Vector2(220f, 22f),
            TextAnchor.MiddleCenter,
            PowerLabelFontSize,
            false);
        powerLabel.text = "POWER";
        StyleHudValueText(powerLabel);

        powerPercentText = CreateText(
            panel,
            "PowerPercentText",
            font,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-18f, -8f),
            new Vector2(100f, 22f),
            TextAnchor.MiddleRight,
            PowerPercentFontSize,
            false);
        powerPercentText.color = PowerAccentColor;
        StyleHudValueText(powerPercentText);

        CreatePowerBar(panel);
        ResetPowerMeter();
    }

    private void CreatePowerBar(RectTransform panel)
    {
        GameObject trackObject = new GameObject("PowerBarTrack", typeof(RectTransform));
        trackObject.layer = 5;
        trackObject.transform.SetParent(panel, false);

        RectTransform trackRect = trackObject.GetComponent<RectTransform>();
        trackRect.localScale = Vector3.one;
        trackRect.localRotation = Quaternion.identity;
        trackRect.anchorMin = new Vector2(0.5f, 0.5f);
        trackRect.anchorMax = new Vector2(0.5f, 0.5f);
        trackRect.pivot = new Vector2(0.5f, 0.5f);
        trackRect.anchoredPosition = new Vector2(0f, -14f);
        trackRect.sizeDelta = new Vector2(PowerBarWidth, PowerBarHeight);

        Image trackImage = trackObject.AddComponent<Image>();
        trackImage.sprite = GetPowerBarTrackSprite();
        trackImage.color = PowerTrackColor;
        trackImage.type = Image.Type.Sliced;
        trackImage.raycastTarget = false;
        trackImage.maskable = false;

        CreatePanelImage(
            trackObject.transform,
            "PowerBarBorder",
            GetPowerBarBorderSprite(),
            HudPanelBorderColor,
            Vector2.zero,
            new Vector2(PowerBarWidth, PowerBarHeight),
            false);

        GameObject fillObject = new GameObject("PowerBarFill", typeof(RectTransform));
        fillObject.layer = 5;
        fillObject.transform.SetParent(trackObject.transform, false);

        powerBarFillRect = fillObject.GetComponent<RectTransform>();
        powerBarFillRect.localScale = Vector3.one;
        powerBarFillRect.localRotation = Quaternion.identity;
        powerBarFillRect.anchorMin = new Vector2(0f, 0.5f);
        powerBarFillRect.anchorMax = new Vector2(0f, 0.5f);
        powerBarFillRect.pivot = new Vector2(0f, 0.5f);
        powerBarFillRect.anchoredPosition = new Vector2(PowerBarInset, 0f);
        powerBarFillRect.sizeDelta = new Vector2(0f, PowerBarFillHeight);

        powerBarFillImage = fillObject.AddComponent<Image>();
        powerBarFillImage.sprite = GetPowerBarFillSprite();
        powerBarFillImage.color = PowerFillColor;
        powerBarFillImage.type = Image.Type.Sliced;
        powerBarFillImage.raycastTarget = false;
        powerBarFillImage.maskable = false;
        powerBarFillImage.enabled = false;

        powerBarFillMaxWidth = PowerBarWidth - PowerBarInset * 2f;
    }

    private void UpdatePowerMeter()
    {
        if (powerPanelObject == null || !powerPanelObject.activeSelf || powerBarFillRect == null)
        {
            return;
        }

        float amount = ballThrowController != null ? Mathf.Clamp01(ballThrowController.CurrentPowerNormalized) : 0f;
        bool hasPower = amount > 0.0001f;

        if (hasPower)
        {
            Vector2 fillSize = powerBarFillRect.sizeDelta;
            fillSize.x = powerBarFillMaxWidth * amount;
            powerBarFillRect.sizeDelta = fillSize;
        }

        if (powerBarFillImage != null && powerBarFillImage.enabled != hasPower)
        {
            powerBarFillImage.enabled = hasPower;
        }

        int percent = Mathf.RoundToInt(amount * 100f);
        if (percent != displayedPowerPercent)
        {
            displayedPowerPercent = percent;

            if (powerPercentText != null)
            {
                powerPercentText.text = percent + "%";
            }
        }
    }

    private void ResetPowerMeter()
    {
        displayedPowerPercent = 0;

        if (powerPercentText != null)
        {
            powerPercentText.text = "0%";
        }

        if (powerBarFillRect != null)
        {
            Vector2 fillSize = powerBarFillRect.sizeDelta;
            fillSize.x = 0f;
            powerBarFillRect.sizeDelta = fillSize;
        }

        if (powerBarFillImage != null)
        {
            powerBarFillImage.enabled = false;
        }
    }

    private static void StyleHudValueText(Text text)
    {
        if (text == null)
        {
            return;
        }

        text.fontStyle = FontStyle.Bold;

        Shadow shadow = text.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = text.gameObject.AddComponent<Shadow>();
        }

        shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
        shadow.effectDistance = new Vector2(0f, -1.5f);
        shadow.useGraphicAlpha = true;
    }

    private static RectTransform CreateHudPanel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2? size = null)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform));
        panel.layer = 5;
        panel.transform.SetParent(parent, false);

        Vector2 panelSize = size ?? new Vector2(HudPanelWidth, HudPanelHeight);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = panelSize;

        CreatePanelImage(
            panel.transform,
            "PanelShadow",
            GetPanelFillSprite(),
            HudPanelShadowColor,
            new Vector2(0f, -3f),
            panelSize,
            true);

        CreatePanelImage(
            panel.transform,
            "PanelFill",
            GetPanelFillSprite(),
            HudPanelColor,
            Vector2.zero,
            panelSize,
            true);

        CreatePanelImage(
            panel.transform,
            "PanelBorder",
            GetPanelBorderSprite(),
            HudPanelBorderColor,
            Vector2.zero,
            panelSize,
            false);

        return rect;
    }

    private static void CreatePanelImage(Transform parent, string name, Sprite sprite, Color color, Vector2 offset, Vector2 size, bool fillCenter)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform));
        imageObject.layer = 5;
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;

        Image image = imageObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = Image.Type.Sliced;
        image.fillCenter = fillCenter;
        image.preserveAspect = false;
        image.raycastTarget = false;
        image.maskable = false;
    }

    private static void CreateHudIcon(Transform parent, string name, Sprite sprite)
    {
        GameObject iconObject = new GameObject(name, typeof(RectTransform));
        iconObject.layer = 5;
        iconObject.transform.SetParent(parent, false);

        RectTransform rect = iconObject.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(HudIconLeftPadding, 0f);
        rect.sizeDelta = new Vector2(HudIconSize, HudIconSize);

        Image image = iconObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.maskable = false;
    }

    private static Sprite GetPanelFillSprite()
    {
        if (hudPanelFillSprite == null)
        {
            hudPanelFillSprite = CreateRoundedPanelSprite(64, 16f, 0f);
        }

        return hudPanelFillSprite;
    }

    private static Sprite GetPanelBorderSprite()
    {
        if (hudPanelBorderSprite == null)
        {
            hudPanelBorderSprite = CreateRoundedPanelSprite(64, 16f, 2.5f);
        }

        return hudPanelBorderSprite;
    }

    private static Sprite GetBasketballIconSprite()
    {
        if (basketballIconSprite == null)
        {
            basketballIconSprite = CreateBasketballIconSprite(128);
        }

        return basketballIconSprite;
    }

    private static Sprite GetTimerIconSprite()
    {
        if (timerIconSprite == null)
        {
            timerIconSprite = CreateTimerIconSprite(128);
        }

        return timerIconSprite;
    }

    private static Sprite GetPowerBarTrackSprite()
    {
        if (powerBarTrackSprite == null)
        {
            powerBarTrackSprite = CreateRoundedPanelSprite(48, 8f, 0f, 8f);
        }

        return powerBarTrackSprite;
    }

    private static Sprite GetPowerBarBorderSprite()
    {
        if (powerBarBorderSprite == null)
        {
            powerBarBorderSprite = CreateRoundedPanelSprite(48, 8f, 1.2f, 8f);
        }

        return powerBarBorderSprite;
    }

    private static Sprite GetPowerBarFillSprite()
    {
        if (powerBarFillSprite == null)
        {
            powerBarFillSprite = CreateRoundedPanelSprite(48, 5f, 0f, 5f);
        }

        return powerBarFillSprite;
    }

    private static Sprite CreateRoundedPanelSprite(int size, float radius, float borderThickness, float sliceBorder = -1f)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        texture.name = borderThickness > 0f ? "HudPanelBorder" : "HudPanelFill";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float half = size * 0.5f;
        bool drawBorder = borderThickness > 0f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - half;
                float py = y + 0.5f - half;
                float distance = RoundedRectDistance(px, py, half, half, radius);

                float alpha;
                if (drawBorder)
                {
                    float outer = Mathf.Clamp01(0.5f - distance);
                    float inner = Mathf.Clamp01(0.5f + distance + borderThickness);
                    alpha = outer * inner;
                }
                else
                {
                    alpha = Mathf.Clamp01(0.5f - distance);
                }

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        float borderPixels = sliceBorder > 0f ? sliceBorder : radius + 6f;
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(borderPixels, borderPixels, borderPixels, borderPixels));
    }

    private static Sprite CreateBasketballIconSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        texture.name = "HudBasketballIcon";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float cx = size * 0.5f;
        float cy = size * 0.5f;
        float radius = size * 0.44f;
        float outlineWidth = size * 0.03f;
        float seamHalf = size * 0.017f;

        Color bright = new Color(1f, 0.62f, 0.22f);
        Color dark = new Color(0.9f, 0.33f, 0.07f);
        Color outlineColor = new Color(0.13f, 0.065f, 0.03f);
        Color seamColor = new Color(0.09f, 0.045f, 0.025f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f;
                float fy = y + 0.5f;
                float sx = fx - cx;
                float sy = fy - cy;
                float distance = Mathf.Sqrt(sx * sx + sy * sy);
                float alpha = Mathf.Clamp01(radius - distance + 0.5f);

                if (alpha <= 0f)
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float gradient = Mathf.Clamp01(0.5f + (-sx * 0.55f + sy * 0.45f) / (radius * 2f));
                Color color = Color.Lerp(dark, bright, gradient);

                float highlightX = (sx + radius * 0.34f) / (radius * 0.72f);
                float highlightY = (sy - radius * 0.42f) / (radius * 0.5f);
                float highlight = Mathf.Clamp01(1f - (highlightX * highlightX + highlightY * highlightY));
                color = Color.Lerp(color, Color.white, highlight * 0.2f);

                float outlineCoverage = Mathf.Clamp01(distance - (radius - outlineWidth) + 0.5f);
                color = Color.Lerp(color, outlineColor, outlineCoverage);

                float seamDistance = Mathf.Min(Mathf.Abs(sx), Mathf.Abs(sy));
                float leftArcX = fx - (cx + radius * 0.9f);
                float leftArc = Mathf.Abs(Mathf.Sqrt(leftArcX * leftArcX + sy * sy) - radius * 1.25f);
                float rightArcX = fx - (cx - radius * 0.9f);
                float rightArc = Mathf.Abs(Mathf.Sqrt(rightArcX * rightArcX + sy * sy) - radius * 1.25f);
                seamDistance = Mathf.Min(seamDistance, Mathf.Min(leftArc, rightArc));

                float seamCoverage = Mathf.Clamp01(seamHalf - seamDistance + 0.5f);
                color = Color.Lerp(color, seamColor, seamCoverage);

                pixels[y * size + x] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f),
                    (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
    }

    private static Sprite CreateTimerIconSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        texture.name = "HudTimerIcon";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float cx = size * 0.5f;
        float cy = size * 0.46f;
        float radius = size * 0.34f;
        float ringHalf = size * 0.027f;

        Color bright = new Color(1f, 0.64f, 0.24f);
        Color dark = new Color(0.91f, 0.35f, 0.09f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f;
                float fy = y + 0.5f;
                float sx = fx - cx;
                float sy = fy - cy;
                float distance = Mathf.Sqrt(sx * sx + sy * sy);

                float ringCoverage = Mathf.Clamp01(ringHalf - Mathf.Abs(distance - radius) + 0.5f);
                float stemCoverage = SegmentCoverage(fx, fy, cx, cy + radius - 2f, cx, cy + radius + 7f, ringHalf * 0.8f);
                float buttonCoverage = SegmentCoverage(fx, fy, cx - size * 0.08f, cy + radius + 9f, cx + size * 0.08f, cy + radius + 9f, ringHalf * 0.9f);
                float longHandCoverage = SegmentCoverage(fx, fy, cx, cy, cx, cy + radius * 0.56f, ringHalf * 0.7f);
                float shortHandCoverage = SegmentCoverage(fx, fy, cx, cy, cx + radius * 0.42f, cy + radius * 0.12f, ringHalf * 0.7f);
                float centerCoverage = Mathf.Clamp01(radius * 0.11f - distance + 0.5f);

                float alpha = Mathf.Max(
                    Mathf.Max(ringCoverage, stemCoverage),
                    Mathf.Max(buttonCoverage, Mathf.Max(longHandCoverage, Mathf.Max(shortHandCoverage, centerCoverage))));

                if (alpha <= 0f)
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float gradient = Mathf.Clamp01(0.5f + (sy / (radius * 2f)));
                Color color = Color.Lerp(dark, bright, gradient);
                color = Color.Lerp(color, Color.white, centerCoverage * 0.3f);

                pixels[y * size + x] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect);
    }

    private static float RoundedRectDistance(float x, float y, float halfWidth, float halfHeight, float radius)
    {
        float qx = Mathf.Abs(x) - (halfWidth - radius);
        float qy = Mathf.Abs(y) - (halfHeight - radius);
        float ax = Mathf.Max(qx, 0f);
        float ay = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
    }

    private static float SegmentCoverage(float px, float py, float ax, float ay, float bx, float by, float halfThickness)
    {
        float vx = bx - ax;
        float vy = by - ay;
        float wx = px - ax;
        float wy = py - ay;
        float lengthSquared = vx * vx + vy * vy;
        float t = lengthSquared > 0.0001f ? Mathf.Clamp01((wx * vx + wy * vy) / lengthSquared) : 0f;
        float dx = wx - vx * t;
        float dy = wy - vy * t;
        float distance = Mathf.Sqrt(dx * dx + dy * dy);
        return Mathf.Clamp01(halfThickness - distance + 0.5f);
    }

    private GameObject CreateOverlayPanel(Transform parent, string name, Color color, bool behind)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform));
        panel.layer = 5;
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panel.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        if (behind)
        {
            panel.transform.SetAsFirstSibling();
        }

        return panel;
    }

    private void BuildStartScreen(Transform parent, Font font)
    {
        Text title = CreateText(
            parent,
            "StartTitle",
            font,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 190f),
            new Vector2(1500f, 160f),
            TextAnchor.MiddleCenter,
            84);
        title.text = "BASKETBALL RUSH";
        startTitleText = title;

        Text subtitle = CreateText(
            parent,
            "StartSubtitle",
            font,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 55f),
            new Vector2(1200f, 60f),
            TextAnchor.MiddleCenter,
            30);
        subtitle.text = "Drag and release to shoot";
        subtitle.color = new Color(0.85f, 0.87f, 0.9f, 1f);
        startSubtitleText = subtitle;

        GameObject buttonObject = new GameObject("StartButton", typeof(RectTransform));
        buttonObject.layer = 5;
        buttonObject.transform.SetParent(parent, false);

        startButtonRect = buttonObject.GetComponent<RectTransform>();
        startButtonRect.anchorMin = new Vector2(0.5f, 0.5f);
        startButtonRect.anchorMax = new Vector2(0.5f, 0.5f);
        startButtonRect.pivot = new Vector2(0.5f, 0.5f);
        startButtonRect.sizeDelta = new Vector2(380f, 96f);
        startButtonRect.anchoredPosition = new Vector2(0f, -80f);

        startButtonImage = buttonObject.AddComponent<Image>();
        startButtonImage.color = StartButtonNormalColor;
        startButtonImage.raycastTarget = false;

        Outline buttonOutline = buttonObject.AddComponent<Outline>();
        buttonOutline.effectColor = new Color(0.24f, 0.08f, 0.02f, 0.9f);
        buttonOutline.effectDistance = new Vector2(2f, -2f);
        buttonOutline.useGraphicAlpha = true;

        Text buttonLabel = CreateText(
            buttonObject.transform,
            "StartButtonLabel",
            font,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(380f, 96f),
            TextAnchor.MiddleCenter,
            36);
        buttonLabel.text = "START GAME";
        buttonLabel.color = Color.white;
        startButtonText = buttonLabel;
    }

    private void ShowGameOver()
    {
        int finalScore = hoopScore != null ? hoopScore.score : lastScore;

        SetPowerPanelVisible(false);
        ResetPowerMeter();

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        if (timeUpText != null)
        {
            timeUpText.SetAllDirty();
        }

        if (gameOverScoreText != null)
        {
            gameOverScoreText.text = "Final Score: " + finalScore;
            gameOverScoreText.gameObject.SetActive(true);
            gameOverScoreText.SetAllDirty();
        }

        if (gameOverHintText != null)
        {
            gameOverHintText.gameObject.SetActive(true);
            gameOverHintText.SetAllDirty();
        }

        Canvas.ForceUpdateCanvases();
    }

    private void ShowScoreFeedback()
    {
        if (scorePopText == null || scoreRect == null)
        {
            return;
        }

        scorePulseTimer = ScorePulseDuration;
        scorePopTimer = ScorePopDuration;

        Color color = ScorePopColor;
        color.a = 1f;
        scorePopText.color = color;
        scorePopText.text = "+1";
        scorePopText.gameObject.SetActive(true);
        scorePopText.SetAllDirty();
        Canvas.ForceUpdateCanvases();
    }

    private void UpdateScoreFeedback()
    {
        if (scoreRect != null && scorePulseTimer > 0f)
        {
            scorePulseTimer -= Time.deltaTime;
            float progress = 1f - Mathf.Clamp01(scorePulseTimer / ScorePulseDuration);
            float scale = 1f + (ScorePulseScale - 1f) * Mathf.Sin(progress * Mathf.PI);
            scoreRect.localScale = scoreBaseScale * scale;

            if (scorePulseTimer <= 0f)
            {
                scoreRect.localScale = scoreBaseScale;
            }
        }

        if (scorePopText != null && scorePopTimer > 0f)
        {
            scorePopTimer -= Time.deltaTime;
            float alpha = Mathf.Clamp01(scorePopTimer / ScorePopFadeDuration);
            Color color = ScorePopColor;
            color.a = alpha;
            scorePopText.color = color;

            if (scorePopTimer <= 0f)
            {
                scorePopText.gameObject.SetActive(false);
            }
        }
    }

    private void ResetScoreFeedback()
    {
        scorePulseTimer = 0f;
        scorePopTimer = 0f;

        if (scoreRect != null)
        {
            scoreRect.localScale = scoreBaseScale;
        }

        if (scorePopText != null)
        {
            scorePopText.gameObject.SetActive(false);
        }
    }

    private void SetupAudio()
    {
        gameAudioSource = GetComponent<AudioSource>();
        if (gameAudioSource == null)
        {
            gameAudioSource = gameObject.AddComponent<AudioSource>();
        }

        gameAudioSource.playOnAwake = false;
        gameAudioSource.loop = false;
        gameAudioSource.spatialBlend = 0f;
        gameAudioSource.volume = 1f;

        if (scoreClip == null) scoreClip = Resources.Load<AudioClip>("Audio/Score");
        if (timeUpClip == null) timeUpClip = Resources.Load<AudioClip>("Audio/TimeUp");
    }

    private void PlayGameSound(AudioClip clip, float volume)
    {
        if (clip == null || gameAudioSource == null)
        {
            return;
        }

        gameAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume * gameAudioVolume));
    }
}
