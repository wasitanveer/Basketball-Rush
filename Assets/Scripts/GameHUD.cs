using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-100)]
public class GameHUD : MonoBehaviour
{
    private const string CanvasObjectName = "BasketballHUDCanvas";
    private const string BestScoreKey = "BasketballRush.BestScore";
    private const float PauseButtonSize = 64f;
    private const float PauseButtonGap = 14f;
    private const float CountdownDuration = 3.6f;
    private const float CountdownGoDuration = 0.6f;
    private const float PanelWidth = 260f;
    private const float PanelHeight = 76f;
    private const float MarginX = 40f;
    private const float MarginY = 26f;

    private static GameHUD instance;
    private static GameObject createdCanvas;
    private static Font sharedFont;
    private static Sprite panelSprite;
    private static Sprite borderSprite;

    public static GameHUD Instance => instance;

    public int BestScoreValue => bestScore;

    public int DisplayedTimeSeconds => displayedTime;

    public bool HasGameStarted => gameStarted;

    public bool IsCountdownActive => countdownActive;

    public bool IsTimerFinished => timerFinished;

    [Header("References")]
    public HoopScoreSystem hoopScore;
    public BallThrowController ballThrowController;

    [Header("Timer")]
    public int startingTime = 60;

    [Header("Appearance")]
    public Font hudFont;
    public Color textColor = Color.white;
    public int fontSize = 34;
    public int timeUpFontSize = 72;

    [Header("Audio")]
    public AudioClip scoreClip;
    public AudioClip timeUpClip;
    [Range(0f, 1f)] public float gameAudioVolume = 0.85f;

    private Text scoreText;
    private Text timerText;
    private Text streakText;
    private Text powerPercentText;
    private Text popupText;
    private Text countdownText;
    private Text startTitleText;
    private Text startSubtitleText;
    private Text startBestText;
    private Text startButtonLabel;
    private Text gameOverTitleText;
    private Text gameOverScoreText;
    private Text gameOverBestText;
    private Text gameOverHintText;
    private Text pauseTitleText;
    private Text pauseHintText;
    private Text resumeButtonLabel;

    private RectTransform scorePanelRect;
    private RectTransform timerPanelRect;
    private RectTransform popupRect;
    private RectTransform startButtonRect;
    private RectTransform resumeButtonRect;
    private RectTransform pauseButtonRect;
    private RectTransform powerFillRect;

    private Image startButtonImage;
    private Image resumeButtonImage;
    private Image pauseButtonImage;
    private Image powerFillImage;

    private GameObject scorePanel;
    private GameObject timerPanel;
    private GameObject powerPanel;
    private GameObject startPanel;
    private GameObject gameOverPanel;
    private GameObject pausePanel;
    private GameObject pauseButton;

    private AudioSource gameAudioSource;
    private AudioClip countdownBeepClip;
    private AudioClip countdownGoClip;

    private float timeRemaining;
    private int displayedTime;
    private int lastScore;
    private int bestScore;
    private int bestAtGameStart;
    private int displayedStreak = -1;
    private bool timerFinished;
    private bool gameStarted;
    private bool isPaused;
    private bool countdownActive;
    private float countdownRemaining;
    private int lastCountdownDisplay = int.MinValue;
    private float countdownPulseTimer;
    private bool bestBeatenThisGame;

    private Vector2 popupBasePosition;
    private float popupTimer;
    private Color popupColor;
    private float popupFontSize;

    private bool pausePointerWasHeld;

    private static readonly Color PanelColor = new Color(0.055f, 0.06f, 0.07f, 0.88f);
    private static readonly Color BorderColor = new Color(1f, 1f, 1f, 0.16f);
    private static readonly Color AccentColor = new Color(1f, 0.55f, 0.14f, 1f);
    private static readonly Color ScorePopColor = new Color(1f, 0.82f, 0.25f, 1f);
    private static readonly Color SwishColor = new Color(0.35f, 1f, 0.55f, 1f);
    private static readonly Color MissColor = new Color(0.9f, 0.4f, 0.32f, 1f);
    private static readonly Color StartButtonColor = new Color(1f, 0.45f, 0.12f, 1f);
    private static readonly Color StartButtonHover = new Color(1f, 0.57f, 0.22f, 1f);
    private static readonly Color StartButtonPressed = new Color(0.86f, 0.33f, 0.06f, 1f);
    private static readonly Color ResumeButtonColor = new Color(0.18f, 0.62f, 0.34f, 1f);
    private static readonly Color ResumeButtonHover = new Color(0.25f, 0.73f, 0.42f, 1f);
    private static readonly Color ResumeButtonPressed = new Color(0.1f, 0.5f, 0.25f, 1f);

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
        if (instance != null || GameObject.Find(CanvasObjectName) != null)
        {
            return;
        }

        if (FindAnyObjectByType<GameHUD>(FindObjectsInactive.Include) != null)
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
        PerformanceTuning.ApplyMenu();

        if (hoopScore == null)
        {
            hoopScore = FindAnyObjectByType<HoopScoreSystem>();
        }

        if (ballThrowController == null)
        {
            ballThrowController = FindAnyObjectByType<BallThrowController>();
        }

        if (hoopScore != null)
        {
            hoopScore.BasketScored += HandleBasketScored;
            hoopScore.ShotMissed += HandleShotMissed;
        }

        bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
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
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (hoopScore != null)
        {
            hoopScore.BasketScored -= HandleBasketScored;
            hoopScore.ShotMissed -= HandleShotMissed;
        }

        if (createdCanvas != null)
        {
            Destroy(createdCanvas);
            createdCanvas = null;
        }
    }

    private void Start()
    {
        gameStarted = false;
        isPaused = false;
        countdownActive = false;
        bestAtGameStart = bestScore;
        bestBeatenThisGame = false;

        if (hoopScore != null)
        {
            hoopScore.ResetScore();
        }

        lastScore = 0;
        timeRemaining = Mathf.Max(0f, startingTime);
        displayedTime = Mathf.CeilToInt(timeRemaining);
        timerFinished = false;

        SetPanelsVisible(false);
        SetPowerVisible(false);
        SetPauseButtonVisible(false);
        SetPausePanelVisible(false);
        SetStartPanelVisible(true);
        SetGameOverVisible(false);
        HideCountdown();
        ResetFeedback();

        if (scoreText != null) scoreText.text = "Score: 0";
        if (timerText != null)
        {
            timerText.text = "Time: " + displayedTime;
            timerText.color = textColor;
        }

        UpdateBestLabels();

        if (ballThrowController != null)
        {
            ballThrowController.RestartBall();
            ballThrowController.canThrow = false;
        }

        RefreshThrowPermission();
        Canvas.ForceUpdateCanvases();
    }

    private void Update()
    {
        if (isPaused)
        {
            SetPowerVisible(false);
            HandlePauseInput();
            HandleRestartKey();
            return;
        }

        UpdateCountdown();
        UpdateTimer();
        UpdateScoreLabel();
        SetPowerVisible(ballThrowController != null && ballThrowController.IsCharging);
        UpdatePowerMeter();
        UpdateFeedback();
        UpdateStreakText();

        HandleStartInput();
        HandlePauseInput();
        HandleRestartInput();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && gameStarted && !timerFinished)
        {
            PauseGame();
        }
    }

    public void StartGame()
    {
        RestartGame();
    }

    public void RestartGame()
    {
        ForceUnpausedState();
        gameStarted = true;
        PerformanceTuning.ApplyGameplay();

        if (gameAudioSource != null)
        {
            gameAudioSource.Stop();
        }

        bestAtGameStart = bestScore;
        bestBeatenThisGame = false;
        timeRemaining = Mathf.Max(0f, startingTime);
        displayedTime = Mathf.CeilToInt(timeRemaining);
        timerFinished = false;

        SetStartPanelVisible(false);
        SetGameOverVisible(false);
        SetPausePanelVisible(false);
        SetPauseButtonVisible(true);
        SetPanelsVisible(true);
        SetPowerVisible(false);

        if (timerText != null)
        {
            timerText.gameObject.SetActive(true);
            timerText.text = "Time: " + displayedTime;
            timerText.color = textColor;
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
        }

        ResetPowerMeter();
        UpdateBestLabels();
        ResetFeedback();

        if (ballThrowController != null)
        {
            ballThrowController.RestartBall();
        }

        StartCountdown();
        Canvas.ForceUpdateCanvases();
    }

    private void StartCountdown()
    {
        countdownActive = true;
        countdownRemaining = CountdownDuration;
        lastCountdownDisplay = int.MinValue;
        countdownPulseTimer = 0.35f;

        if (countdownText != null)
        {
            countdownText.text = "3";
            countdownText.color = new Color(1f, 0.78f, 0.3f, 1f);
            countdownText.gameObject.SetActive(true);
        }

        RefreshThrowPermission();
    }

    private void UpdateCountdown()
    {
        if (!countdownActive)
        {
            return;
        }

        countdownRemaining -= Time.deltaTime;

        int display;
        if (countdownRemaining > CountdownDuration - 1f)
        {
            display = 3;
        }
        else if (countdownRemaining > CountdownGoDuration + 1f)
        {
            display = 2;
        }
        else if (countdownRemaining > CountdownGoDuration)
        {
            display = 1;
        }
        else
        {
            display = 0;
        }

        if (display != lastCountdownDisplay)
        {
            lastCountdownDisplay = display;
            countdownPulseTimer = 0.35f;

            if (countdownText != null)
            {
                countdownText.text = display == 0 ? "GO!" : display.ToString();
                countdownText.color = display == 0
                    ? new Color(0.4f, 1f, 0.6f, 1f)
                    : new Color(1f, 0.78f, 0.3f, 1f);
            }

            PlaySound(display == 0 ? countdownGoClip : countdownBeepClip, display == 0 ? 0.9f : 0.75f);
        }

        if (countdownPulseTimer > 0f && countdownText != null)
        {
            countdownPulseTimer -= Time.deltaTime;
            float progress = 1f - Mathf.Clamp01(countdownPulseTimer / 0.35f);
            countdownText.rectTransform.localScale = Vector3.one * (1f + 0.32f * Mathf.Sin(progress * Mathf.PI));
        }

        if (countdownRemaining <= 0f)
        {
            countdownActive = false;
            HideCountdown();
            RefreshThrowPermission();
        }
    }

    private void HideCountdown()
    {
        countdownActive = false;

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
            countdownText.rectTransform.localScale = Vector3.one;
        }
    }

    private void UpdateTimer()
    {
        if (!gameStarted || timerFinished || countdownActive)
        {
            return;
        }

        timeRemaining -= Time.deltaTime;

        if (timeRemaining <= 0f)
        {
            timeRemaining = 0f;
            timerFinished = true;

            if (timerText != null)
            {
                timerText.text = "Time: 0";
                timerText.color = MissColor;
            }

            ShowGameOver();
            PlaySound(timeUpClip, 0.9f);
            SetPauseButtonVisible(false);
            RefreshThrowPermission();
            return;
        }

        int newDisplayed = Mathf.CeilToInt(timeRemaining);
        if (newDisplayed != displayedTime)
        {
            displayedTime = newDisplayed;

            if (timerText != null)
            {
                timerText.text = "Time: " + displayedTime;
                timerText.color = displayedTime <= 10 ? MissColor : textColor;
            }
        }
    }

    private void UpdateScoreLabel()
    {
        if (hoopScore == null || scoreText == null)
        {
            return;
        }

        int currentScore = hoopScore.score;
        if (currentScore == lastScore)
        {
            return;
        }

        if (currentScore > lastScore && currentScore > bestScore)
        {
            bestScore = currentScore;
            bestBeatenThisGame = currentScore > bestAtGameStart;
            PlayerPrefs.SetInt(BestScoreKey, bestScore);
            PlayerPrefs.Save();
            UpdateBestLabels();
        }

        lastScore = currentScore;
        scoreText.text = "Score: " + currentScore;

        if (timerFinished && gameOverScoreText != null && gameOverScoreText.gameObject.activeSelf)
        {
            gameOverScoreText.text = "Final Score: " + currentScore;
        }
    }

    private void UpdateStreakText()
    {
        if (streakText == null)
        {
            return;
        }

        int streak = hoopScore != null ? hoopScore.Streak : 0;
        if (streak == displayedStreak)
        {
            return;
        }

        displayedStreak = streak;

        if (streak >= 2)
        {
            streakText.gameObject.SetActive(true);
            streakText.text = "STREAK x" + streak;
            streakText.color = streak >= 6
                ? new Color(1f, 0.32f, 0.18f, 1f)
                : streak >= 4
                    ? new Color(1f, 0.52f, 0.14f, 1f)
                    : new Color(1f, 0.82f, 0.25f, 1f);
        }
        else
        {
            streakText.gameObject.SetActive(false);
        }
    }

    private void HandleBasketScored(int points, bool swish, int streak)
    {
        PlaySound(scoreClip, 0.9f);
        ShowPopup(
            "+" + Mathf.Max(1, points) + (swish ? " SWISH!" : string.Empty) +
            (streak >= 3 ? "  x" + streak : string.Empty),
            swish ? SwishColor : ScorePopColor,
            fontSize + (swish ? 12 : 8));
    }

    private void HandleShotMissed()
    {
        if (!gameStarted || timerFinished)
        {
            return;
        }

        ShowPopup("MISS", MissColor, fontSize + 4);
    }

    private void ShowPopup(string message, Color color, float size)
    {
        if (popupText == null)
        {
            return;
        }

        popupTimer = 0.9f;
        popupColor = color;
        popupFontSize = size;
        popupText.text = message;
        popupText.fontSize = Mathf.RoundToInt(size);
        popupText.color = color;
        popupText.gameObject.SetActive(true);

        if (popupRect != null)
        {
            popupRect.anchoredPosition = popupBasePosition;
            popupRect.localScale = Vector3.one;
        }
    }

    private void UpdateFeedback()
    {
        if (popupText == null || popupTimer <= 0f)
        {
            return;
        }

        popupTimer -= Time.deltaTime;
        float progress = 1f - Mathf.Clamp01(popupTimer / 0.9f);

        if (popupRect != null)
        {
            popupRect.anchoredPosition = popupBasePosition + new Vector2(0f, progress * 26f);
            popupRect.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(Mathf.Clamp01(progress * 4f) * Mathf.PI));
        }

        Color color = popupColor;
        color.a = Mathf.Clamp01(popupTimer / 0.3f);
        popupText.color = color;

        if (popupTimer <= 0f)
        {
            popupText.gameObject.SetActive(false);

            if (popupRect != null)
            {
                popupRect.anchoredPosition = popupBasePosition;
                popupRect.localScale = Vector3.one;
            }
        }
    }

    private void ResetFeedback()
    {
        popupTimer = 0f;
        displayedStreak = -1;

        if (popupText != null)
        {
            popupText.gameObject.SetActive(false);
        }

        if (popupRect != null)
        {
            popupRect.anchoredPosition = popupBasePosition;
            popupRect.localScale = Vector3.one;
        }

        if (streakText != null)
        {
            streakText.gameObject.SetActive(false);
        }
    }

    private void UpdateBestLabels()
    {
        if (startBestText != null)
        {
            startBestText.text = "BEST: " + bestScore;
        }

        if (gameOverBestText != null)
        {
            gameOverBestText.text = (bestBeatenThisGame ? "NEW BEST! " : "BEST: ") + bestScore;
            gameOverBestText.color = bestBeatenThisGame
                ? new Color(0.4f, 1f, 0.6f, 1f)
                : new Color(1f, 0.82f, 0.25f, 1f);
        }
    }

    private void ShowGameOver()
    {
        SetPowerVisible(false);
        ResetPowerMeter();
        SetPanelsVisible(false);
        SetPauseButtonVisible(false);

        if (gameOverTitleText != null)
        {
            gameOverTitleText.text = "TIME UP!";
        }

        if (gameOverScoreText != null)
        {
            gameOverScoreText.text = "Final Score: " + lastScore;
        }

        UpdateBestLabels();
        SetGameOverVisible(true);
        PerformanceTuning.ApplyMenu();
    }

    private void PauseGame()
    {
        if (!gameStarted || timerFinished || isPaused)
        {
            return;
        }

        isPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        PerformanceTuning.ApplyPaused();

        if (ballThrowController != null)
        {
            ballThrowController.CancelAim();
        }

        SetPauseButtonVisible(false);
        SetPausePanelVisible(true);
        ResetPowerMeter();
        RefreshThrowPermission();
    }

    private void ResumeGame()
    {
        if (!isPaused)
        {
            return;
        }

        isPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        PerformanceTuning.ApplyGameplay();
        pausePointerWasHeld = false;

        SetPausePanelVisible(false);
        SetPauseButtonVisible(gameStarted && !timerFinished);
        RefreshThrowPermission();
    }

    private void TogglePause()
    {
        if (isPaused)
        {
            ResumeGame();
        }
        else if (gameStarted && !timerFinished)
        {
            PauseGame();
        }
    }

    private void ForceUnpausedState()
    {
        isPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        pausePointerWasHeld = false;
        SetPausePanelVisible(false);
        SetPauseButtonVisible(false);
    }

    private void RefreshThrowPermission()
    {
        if (ballThrowController == null)
        {
            return;
        }

        ballThrowController.canThrow = gameStarted && !timerFinished && !countdownActive && !isPaused;
    }

    private void HandleStartInput()
    {
        if (startPanel == null || !startPanel.activeSelf || startButtonRect == null)
        {
            return;
        }

        if (!TryReadPointer(out Vector2 pointerPosition, out bool held, out bool released))
        {
            return;
        }

        bool overButton = RectTransformUtility.RectangleContainsScreenPoint(startButtonRect, pointerPosition, null);
        bool pressed = overButton && held;

        if (startButtonImage != null)
        {
            startButtonImage.color = pressed
                ? StartButtonPressed
                : (overButton ? StartButtonHover : StartButtonColor);
        }

        if (startButtonRect != null)
        {
            startButtonRect.localScale = Vector3.one * (pressed ? 0.97f : (overButton ? 1.03f : 1f));
        }

        if (overButton && released)
        {
            StartGame();
        }
    }

    private void HandlePauseInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.pKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
        {
            TogglePause();
            return;
        }

        if (!TryReadPointer(out Vector2 pointerPosition, out bool held, out bool released))
        {
            pausePointerWasHeld = false;
            return;
        }

        if (isPaused)
        {
            if (resumeButtonRect == null)
            {
                return;
            }

            bool overButton = RectTransformUtility.RectangleContainsScreenPoint(resumeButtonRect, pointerPosition, null);

            if (resumeButtonImage != null)
            {
                resumeButtonImage.color = overButton && held
                    ? ResumeButtonPressed
                    : (overButton ? ResumeButtonHover : ResumeButtonColor);
            }

            if (resumeButtonRect != null)
            {
                resumeButtonRect.localScale = Vector3.one * (overButton && held ? 0.97f : (overButton ? 1.03f : 1f));
            }

            if (overButton && released)
            {
                ResumeGame();
            }

            return;
        }

        if (pauseButton == null || !pauseButton.activeSelf || pauseButtonRect == null)
        {
            pausePointerWasHeld = false;
            return;
        }

        bool overPause = RectTransformUtility.RectangleContainsScreenPoint(pauseButtonRect, pointerPosition, null);

        if (pauseButtonImage != null)
        {
            pauseButtonImage.color = overPause
                ? new Color(0.16f, 0.17f, 0.2f, 0.95f)
                : PanelColor;
        }

        if (overPause && held && !pausePointerWasHeld)
        {
            pausePointerWasHeld = true;
            PauseGame();
            return;
        }

        if (!held)
        {
            pausePointerWasHeld = false;
        }
    }

    private void HandleRestartKey()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            RestartGame();
        }
    }

    private void HandleRestartInput()
    {
        if (!timerFinished)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            RestartGame();
            return;
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.wasReleasedThisFrame)
        {
            RestartGame();
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasReleasedThisFrame)
        {
            RestartGame();
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

    private void UpdatePowerMeter()
    {
        if (powerPanel == null || !powerPanel.activeSelf || powerFillRect == null)
        {
            return;
        }

        float amount = ballThrowController != null ? Mathf.Clamp01(ballThrowController.CurrentPowerNormalized) : 0f;
        bool hasPower = amount > 0.0001f;

        if (hasPower)
        {
            Vector2 size = powerFillRect.sizeDelta;
            size.x = 354f * amount;
            powerFillRect.sizeDelta = size;
        }

        if (powerFillImage != null && powerFillImage.enabled != hasPower)
        {
            powerFillImage.enabled = hasPower;
        }

        int percent = Mathf.RoundToInt(amount * 100f);
        if (powerPercentText != null && powerPercentText.text != percent + "%")
        {
            powerPercentText.text = percent + "%";
        }
    }

    private void ResetPowerMeter()
    {
        if (powerPercentText != null)
        {
            powerPercentText.text = "0%";
        }

        if (powerFillRect != null)
        {
            Vector2 size = powerFillRect.sizeDelta;
            size.x = 0f;
            powerFillRect.sizeDelta = size;
        }

        if (powerFillImage != null)
        {
            powerFillImage.enabled = false;
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
        hudRoot.offsetMin = Vector2.zero;
        hudRoot.offsetMax = Vector2.zero;
        safeAreaObject.AddComponent<SafeAreaFitter>();

        Font font = ResolveFont();

        scorePanel = CreatePanel(hudRoot, "ScorePanel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(MarginX, -MarginY), new Vector2(PanelWidth, PanelHeight));
        scorePanelRect = scorePanel.GetComponent<RectTransform>();
        scoreText = CreateText(scorePanel.transform, "ScoreText", font, TextAnchor.MiddleCenter, fontSize, Vector2.zero, new Vector2(PanelWidth - 24f, PanelHeight), textColor);
        scoreText.text = "Score: 0";

        timerPanel = CreatePanel(hudRoot, "TimerPanel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-MarginX, -MarginY), new Vector2(PanelWidth, PanelHeight));
        timerPanelRect = timerPanel.GetComponent<RectTransform>();
        timerText = CreateText(timerPanel.transform, "TimerText", font, TextAnchor.MiddleCenter, fontSize, Vector2.zero, new Vector2(PanelWidth - 24f, PanelHeight), textColor);
        timerText.text = "Time: " + startingTime;

        streakText = CreateText(hudRoot, "StreakText", font, TextAnchor.MiddleLeft, 28, new Vector2(MarginX + 180f, -(MarginY + PanelHeight + 46f)), new Vector2(360f, 40f), ScorePopColor, new Vector2(0f, 1f));
        streakText.text = "STREAK x2";
        streakText.gameObject.SetActive(false);

        popupText = CreateText(hudRoot, "PopupText", font, TextAnchor.MiddleLeft, fontSize + 8, new Vector2(MarginX + PanelWidth + 190f, -(MarginY + PanelHeight * 0.5f)), new Vector2(360f, 60f), ScorePopColor, new Vector2(0f, 1f));
        popupRect = popupText.rectTransform;
        popupBasePosition = popupRect.anchoredPosition;
        popupText.gameObject.SetActive(false);

        countdownText = CreateText(hudRoot, "CountdownText", font, TextAnchor.MiddleCenter, 160, new Vector2(0f, 40f), new Vector2(1200f, 260f), new Color(1f, 0.78f, 0.3f, 1f));
        countdownText.text = "3";
        countdownText.gameObject.SetActive(false);

        BuildPowerMeter(hudRoot, font);
        BuildPauseButton(hudRoot, font);

        startPanel = CreateOverlay(hudRoot, "StartPanel", new Color(0.03f, 0.04f, 0.06f, 0.82f));
        BuildStartScreen(startPanel.transform, font);

        gameOverPanel = CreateOverlay(hudRoot, "GameOverPanel", new Color(0.03f, 0.04f, 0.06f, 0.82f));
        BuildGameOverScreen(gameOverPanel.transform, font);

        pausePanel = CreateOverlay(hudRoot, "PausePanel", new Color(0.02f, 0.03f, 0.05f, 0.8f));
        BuildPauseScreen(pausePanel.transform, font);

        SetGameOverVisible(false);
        SetPausePanelVisible(false);
        SetPauseButtonVisible(false);

        Canvas.ForceUpdateCanvases();
    }

    private void BuildPowerMeter(RectTransform parent, Font font)
    {
        powerPanel = CreatePanel(
            parent,
            "PowerPanel",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 32f),
            new Vector2(420f, 80f));

        Text label = CreateText(powerPanel.transform, "PowerLabel", font, TextAnchor.MiddleCenter, 22, new Vector2(0f, 24f), new Vector2(200f, 24f), textColor);
        label.text = "POWER";

        powerPercentText = CreateText(powerPanel.transform, "PowerPercent", font, TextAnchor.MiddleRight, 18, new Vector2(185f, 24f), new Vector2(80f, 24f), AccentColor);
        powerPercentText.text = "0%";

        GameObject track = CreateImage(powerPanel.transform, "PowerTrack", new Vector2(360f, 16f), new Vector2(0f, -14f), new Color(0.015f, 0.018f, 0.022f, 0.92f));
        RectTransform trackRect = track.GetComponent<RectTransform>();

        GameObject border = CreateImage(track.transform, "PowerBorder", new Vector2(360f, 16f), Vector2.zero, BorderColor);
        border.GetComponent<Image>().sprite = GetBorderSprite();
        border.GetComponent<Image>().type = Image.Type.Sliced;

        GameObject fill = CreateImage(track.transform, "PowerFill", new Vector2(0f, 10f), Vector2.zero, new Color(1f, 0.52f, 0.1f, 1f));
        powerFillRect = fill.GetComponent<RectTransform>();
        powerFillRect.anchorMin = new Vector2(0f, 0.5f);
        powerFillRect.anchorMax = new Vector2(0f, 0.5f);
        powerFillRect.pivot = new Vector2(0f, 0.5f);
        powerFillRect.anchoredPosition = new Vector2(3f, 0f);
        powerFillImage = fill.GetComponent<Image>();
        powerFillImage.enabled = false;
    }

    private void BuildPauseButton(Transform parent, Font font)
    {
        pauseButton = CreatePanel(
            parent,
            "PauseButton",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-(MarginX + PanelWidth + PauseButtonGap), -MarginY),
            new Vector2(PauseButtonSize, PauseButtonSize));

        pauseButtonRect = pauseButton.GetComponent<RectTransform>();
        pauseButtonImage = pauseButton.GetComponent<Image>();
        pauseButtonImage.color = PanelColor;

        Text icon = CreateText(pauseButton.transform, "PauseIcon", font, TextAnchor.MiddleCenter, 30, Vector2.zero, new Vector2(PauseButtonSize, PauseButtonSize), textColor);
        icon.text = "II";
        icon.fontStyle = FontStyle.Bold;

        pauseButton.SetActive(false);
    }

    private void BuildStartScreen(Transform parent, Font font)
    {
        startTitleText = CreateText(parent, "StartTitle", font, TextAnchor.MiddleCenter, 84, new Vector2(0f, 190f), new Vector2(1500f, 160f), textColor);
        startTitleText.text = "BASKETBALL RUSH";

        startSubtitleText = CreateText(parent, "StartSubtitle", font, TextAnchor.MiddleCenter, 30, new Vector2(0f, 55f), new Vector2(1200f, 60f), new Color(0.85f, 0.87f, 0.9f, 1f));
        startSubtitleText.text = "Drag and release to shoot";

        startBestText = CreateText(parent, "StartBest", font, TextAnchor.MiddleCenter, 28, new Vector2(0f, 2f), new Vector2(1000f, 40f), new Color(1f, 0.82f, 0.25f, 1f));
        startBestText.text = "BEST: " + bestScore;

        HudButton button = CreateButton(parent, "StartButton", font, "START GAME", new Vector2(0f, -80f), new Vector2(380f, 96f), StartButtonColor);
        startButtonRect = button.Rect;
        startButtonImage = button.Image;
        startButtonLabel = button.Label;
    }

    private void BuildGameOverScreen(Transform parent, Font font)
    {
        gameOverTitleText = CreateText(parent, "GameOverTitle", font, TextAnchor.MiddleCenter, timeUpFontSize, new Vector2(0f, 150f), new Vector2(1000f, 150f), textColor);
        gameOverTitleText.text = "TIME UP!";

        gameOverScoreText = CreateText(parent, "GameOverScore", font, TextAnchor.MiddleCenter, 46, new Vector2(0f, -15f), new Vector2(1000f, 80f), textColor);
        gameOverScoreText.text = "Final Score: 0";

        gameOverBestText = CreateText(parent, "GameOverBest", font, TextAnchor.MiddleCenter, 30, new Vector2(0f, -90f), new Vector2(1000f, 50f), ScorePopColor);
        gameOverBestText.text = "BEST: 0";

        gameOverHintText = CreateText(parent, "GameOverHint", font, TextAnchor.MiddleCenter, 26, new Vector2(0f, -160f), new Vector2(1000f, 50f), new Color(0.76f, 0.78f, 0.82f, 1f));
        gameOverHintText.text = "Press R or tap to play again";
    }

    private void BuildPauseScreen(Transform parent, Font font)
    {
        pauseTitleText = CreateText(parent, "PauseTitle", font, TextAnchor.MiddleCenter, 84, new Vector2(0f, 150f), new Vector2(1000f, 120f), textColor);
        pauseTitleText.text = "PAUSED";

        HudButton button = CreateButton(parent, "ResumeButton", font, "RESUME", new Vector2(0f, -60f), new Vector2(380f, 96f), ResumeButtonColor);
        resumeButtonRect = button.Rect;
        resumeButtonImage = button.Image;
        resumeButtonLabel = button.Label;

        pauseHintText = CreateText(parent, "PauseHint", font, TextAnchor.MiddleCenter, 24, new Vector2(0f, -160f), new Vector2(1000f, 50f), new Color(0.76f, 0.78f, 0.82f, 1f));
        pauseHintText.text = "Press P or Esc, or tap resume";
    }

    private HudButton CreateButton(Transform parent, string name, Font font, string label, Vector2 position, Vector2 size, Color color)
    {
        GameObject button = new GameObject(name, typeof(RectTransform));
        button.layer = 5;
        button.transform.SetParent(parent, false);

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = button.AddComponent<Image>();
        image.sprite = GetPanelSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;

        Outline outline = button.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
        outline.effectDistance = new Vector2(2f, -2f);

        Text text = CreateText(button.transform, "Label", font, TextAnchor.MiddleCenter, 36, Vector2.zero, size, Color.white);
        text.text = label;
        text.fontStyle = FontStyle.Bold;

        return new HudButton
        {
            Rect = rect,
            Image = image,
            Label = text
        };
    }

    private GameObject CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform));
        panel.layer = 5;
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = panel.AddComponent<Image>();
        image.sprite = GetPanelSprite();
        image.type = Image.Type.Sliced;
        image.color = PanelColor;
        image.raycastTarget = false;

        GameObject border = CreateImage(rect, "Border", size, Vector2.zero, BorderColor);
        border.GetComponent<Image>().sprite = GetBorderSprite();
        border.GetComponent<Image>().type = Image.Type.Sliced;

        GameObject accent = CreateImage(rect, "Accent", new Vector2(size.x - 20f, 3f), new Vector2(0f, -size.y * 0.5f + 2f), new Color(1f, 0.55f, 0.14f, 0.8f));
        accent.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0f);
        accent.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0f);
        accent.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);

        return panel;
    }

    private GameObject CreateOverlay(Transform parent, string name, Color color)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform));
        panel.layer = 5;
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = panel.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return panel;
    }

    private Text CreateText(Transform parent, string name, Font font, TextAnchor alignment, int size, Vector2 position, Vector2 sizeDelta, Color color, Vector2? anchorOverride = null)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.layer = 5;
        textObject.transform.SetParent(parent, false);

        Vector2 anchor = anchorOverride ?? new Vector2(0.5f, 0.5f);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;

        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.supportRichText = false;

        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        return text;
    }

    private static GameObject CreateImage(Transform parent, string name, Vector2 size, Vector2 position, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform));
        imageObject.layer = 5;
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = imageObject.AddComponent<Image>();
        image.sprite = GetPanelSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return imageObject;
    }

    private void SetPanelsVisible(bool visible)
    {
        if (scorePanel != null) scorePanel.SetActive(visible);
        if (timerPanel != null) timerPanel.SetActive(visible);
    }

    private void SetPowerVisible(bool visible)
    {
        if (powerPanel != null) powerPanel.SetActive(visible);
    }

    private void SetStartPanelVisible(bool visible)
    {
        if (startPanel != null) startPanel.SetActive(visible);
    }

    private void SetGameOverVisible(bool visible)
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(visible);
    }

    private void SetPausePanelVisible(bool visible)
    {
        if (pausePanel != null) pausePanel.SetActive(visible);
    }

    private void SetPauseButtonVisible(bool visible)
    {
        if (pauseButton != null) pauseButton.SetActive(visible);
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

        sharedFont = Resources.Load<Font>("HUDFont");

        if (sharedFont == null)
        {
            try
            {
                sharedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch
            {
                sharedFont = null;
            }
        }

        if (sharedFont == null)
        {
            try
            {
                sharedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch
            {
                sharedFont = null;
            }
        }

        if (sharedFont == null)
        {
            string[] candidates = { "Segoe UI", "Arial", "Tahoma", "Verdana", "Helvetica" };
            for (int i = 0; i < candidates.Length && sharedFont == null; i++)
            {
                try
                {
                    sharedFont = Font.CreateDynamicFontFromOSFont(candidates[i], Mathf.Max(16, fontSize));
                }
                catch
                {
                    sharedFont = null;
                }
            }
        }

        return sharedFont;
    }

    private static Sprite GetPanelSprite()
    {
        if (panelSprite == null)
        {
            panelSprite = CreateRoundedSprite(64, 14f, 0f);
        }

        return panelSprite;
    }

    private static Sprite GetBorderSprite()
    {
        if (borderSprite == null)
        {
            borderSprite = CreateRoundedSprite(64, 14f, 2.2f);
        }

        return borderSprite;
    }

    private static Sprite CreateRoundedSprite(int size, float radius, float borderThickness)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        texture.name = borderThickness > 0f ? "HudBorder" : "HudPanel";
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - half;
                float py = y + 0.5f - half;
                float qx = Mathf.Abs(px) - (half - radius);
                float qy = Mathf.Abs(py) - (half - radius);
                float ax = Mathf.Max(qx, 0f);
                float ay = Mathf.Max(qy, 0f);
                float distance = Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;

                float alpha;
                if (borderThickness > 0f)
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

        float slice = borderThickness > 0f ? radius + 4f : radius + 2f;
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(slice, slice, slice, slice));
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

        if (countdownBeepClip == null) countdownBeepClip = CreateToneClip("CountdownBeep", 880f, 0.12f);
        if (countdownGoClip == null) countdownGoClip = CreateToneClip("CountdownGo", 1320f, 0.22f);
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null || gameAudioSource == null)
        {
            return;
        }

        gameAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume * gameAudioVolume));
    }

    private static AudioClip CreateToneClip(string clipName, float frequency, float duration)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * duration));
        float[] samples = new float[sampleCount];
        int fadeSamples = Mathf.Max(1, Mathf.RoundToInt(sampleRate * 0.008f));

        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / sampleRate;
            float fadeIn = Mathf.Min(1f, (float)i / fadeSamples);
            float fadeOut = Mathf.Min(1f, (float)(sampleCount - 1 - i) / fadeSamples);
            float envelope = Mathf.Min(fadeIn, fadeOut);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * 0.5f * envelope;
        }

        AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private class HudButton
    {
        public RectTransform Rect;
        public Image Image;
        public Text Label;
    }
}
