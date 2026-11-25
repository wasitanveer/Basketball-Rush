using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class NPCInteractionSystem : MonoBehaviour
{
    public float interactionRadius = 2.5f;
    public float interactionAngle = 35f;
    public float responseDuration = 2.5f;
    public string promptMessage = "Press E to talk";

    GameObject canvasObject;
    Text promptText;
    Text responseText;
    bool promptVisible;
    float responseTimer;
    NPCInteraction currentTarget;

    public NPCInteraction CurrentTarget => currentTarget;
    public bool HasPrompt => promptText != null && promptText.gameObject.activeSelf;
    public string CurrentResponse => responseText != null && responseText.gameObject.activeSelf ? responseText.text : null;

    void Awake()
    {
        BuildUI();
    }

    void OnDestroy()
    {
        if (canvasObject != null) Destroy(canvasObject);
    }

    void Update()
    {
        if (!GameplayActive())
        {
            currentTarget = null;
            HidePrompt();
            HideResponse();
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            currentTarget = null;
            HidePrompt();
            HideResponse();
            return;
        }

        currentTarget = FindTarget(cam);
        SetPromptVisible(currentTarget != null);

        if (currentTarget != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            InteractWithCurrent();
        }

        if (responseTimer > 0f)
        {
            responseTimer -= Time.unscaledDeltaTime;
            if (responseTimer <= 0f) HideResponse();
        }
    }

    public void InteractWithCurrent()
    {
        if (currentTarget == null) return;
        ShowResponse(currentTarget.Interact());
    }

    bool GameplayActive()
    {
        GameHUD hud = GameHUD.Instance;
        if (hud == null) return false;
        if (!hud.HasGameStarted || hud.IsCountdownActive || hud.IsTimerFinished) return false;
        return Time.timeScale > 0.0001f;
    }

    NPCInteraction FindTarget(Camera cam)
    {
        NPCInteraction best = null;
        float bestDistance = float.MaxValue;
        Vector3 camPosition = cam.transform.position;
        Vector3 flatForward = cam.transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f) return null;
        flatForward.Normalize();

        float minDot = Mathf.Cos(interactionAngle * Mathf.Deg2Rad);
        float radiusSqr = interactionRadius * interactionRadius;

        for (int i = 0; i < NPCInteraction.Active.Count; i++)
        {
            NPCInteraction npc = NPCInteraction.Active[i];
            if (npc == null || !npc.isActiveAndEnabled) continue;

            Vector3 toNpc = npc.transform.position - camPosition;
            toNpc.y = 0f;
            float distanceSqr = toNpc.sqrMagnitude;
            if (distanceSqr > radiusSqr || distanceSqr < 0.0001f) continue;

            Vector3 flatToNpc = toNpc.normalized;
            if (Vector3.Dot(flatForward, flatToNpc) < minDot) continue;

            if (distanceSqr < bestDistance)
            {
                bestDistance = distanceSqr;
                best = npc;
            }
        }

        return best;
    }

    void ShowResponse(string line)
    {
        if (responseText == null) return;
        responseText.text = line;
        responseText.gameObject.SetActive(true);
        responseTimer = responseDuration;
    }

    void HideResponse()
    {
        responseTimer = 0f;
        if (responseText != null) responseText.gameObject.SetActive(false);
    }

    void SetPromptVisible(bool visible)
    {
        if (promptText == null || promptVisible == visible) return;
        promptVisible = visible;
        promptText.text = promptMessage;
        promptText.gameObject.SetActive(visible);
    }

    void HidePrompt()
    {
        SetPromptVisible(false);
    }

    void BuildUI()
    {
        canvasObject = new GameObject("NPCInteractionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.layer = 5;
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Font font = Resources.Load<Font>("HUDFont");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 24);

        promptText = CreateText(canvasObject.transform, "InteractionPrompt", font, 28,
            new Vector2(0f, 140f), new Vector2(620f, 50f), new Color(1f, 1f, 1f, 0.95f));
        promptText.text = promptMessage;
        promptText.gameObject.SetActive(false);

        responseText = CreateText(canvasObject.transform, "InteractionResponse", font, 32,
            new Vector2(0f, 205f), new Vector2(760f, 60f), new Color(1f, 0.85f, 0.35f, 1f));
        responseText.gameObject.SetActive(false);
    }

    static Text CreateText(Transform parent, string name, Font font, int size, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.layer = 5;
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return text;
    }
}
