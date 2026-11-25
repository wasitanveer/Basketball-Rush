using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[DefaultExecutionOrder(-500)]
public class ArenaVisuals : MonoBehaviour
{
    private static ArenaVisuals instance;

    private HoopScoreSystem hoopScore;
    private GameHUD hud;

    private Text scoreText;
    private Text timeText;
    private Text bestText;
    private Text statusText;

    private Light rimFlashLight;
    private float rimFlashTimer;
    private float nextScoreboardRefresh;

    private int lastScore = int.MinValue;
    private int lastTime = int.MinValue;
    private int lastBest = int.MinValue;
    private int lastStatus = int.MinValue;

    private bool mobile;
    private Material ledMaterial;
    private Texture2D crowdTexture;
    private Texture2D bannerTexture;
    private Texture2D ledTexture;
    private Texture2D emblemTexture;
    private Texture2D softCircleTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<ArenaVisuals>() != null)
        {
            return;
        }

        if (GameObject.Find("ArenaVisualsDisabled") != null)
        {
            return;
        }

        GameObject arenaObject = new GameObject("ArenaVisuals");
        arenaObject.AddComponent<ArenaVisuals>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        mobile = Application.isMobilePlatform;
        hoopScore = FindAnyObjectByType<HoopScoreSystem>();

        PolishExistingScene();
        ApplyPostProcessing();
        CreateArenaLights();
    }

    private void Start()
    {
        crowdTexture = CreateCrowdTexture();
        bannerTexture = CreateBannerTexture();
        ledTexture = CreateLedTexture();
        emblemTexture = CreateEmblemTexture();
        softCircleTexture = CreateSoftCircleTexture();

        BuildStands();
        BuildLedBoards();
        BuildBanners();
        BuildJumbotron();
        BuildCourtLogo();
        BuildDust();

        CreateReflectionProbe();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        if (hoopScore != null)
        {
            hoopScore.BasketScored -= HandleBasketScored;
        }
    }

    private void Update()
    {
        if (rimFlashLight != null && rimFlashTimer > 0f)
        {
            rimFlashTimer -= Time.deltaTime;
            float amount = Mathf.Clamp01(rimFlashTimer / 0.55f);
            rimFlashLight.intensity = 8f * amount * amount;
        }

        if (ledMaterial != null)
        {
            Vector2 offset = new Vector2(Time.time * 0.12f, 0f);
            ledMaterial.SetTextureOffset("_BaseMap", offset);

            if (ledMaterial.HasProperty("_EmissionMap"))
            {
                ledMaterial.SetTextureOffset("_EmissionMap", offset);
            }
        }

        RefreshScoreboard();
    }

    private void HandleBasketScored(int points, bool swish, int streak)
    {
        rimFlashTimer = 0.55f;
    }

    private void PolishExistingScene()
    {
        ConfigureCourt();
        ConfigureBall();
        ConfigureRim();
        ConfigureBackboard();
        ConfigureHoopPole();
        ConfigureNet();
        ConfigureCeilingLights();
        ConfigureCourtLines();
    }

    private void ConfigureCourt()
    {
        Renderer courtRenderer = FindRenderer("Court");
        if (courtRenderer == null)
        {
            return;
        }

        Material material = courtRenderer.material;
        material.SetFloat("_Smoothness", 0.44f);
        material.SetFloat("_Metallic", 0.03f);
        material.SetFloat("_EnvironmentReflections", 1f);

        Texture2D woodNormal = CreateWoodNormalTexture(128);
        material.SetTexture("_BumpMap", woodNormal);
        material.SetFloat("_BumpScale", 0.32f);
        material.EnableKeyword("_NORMALMAP");

        if (material.HasProperty("_BaseMap") && material.HasProperty("_BumpMap"))
        {
            material.SetTextureScale("_BumpMap", material.GetTextureScale("_BaseMap"));
        }
    }

    private void ConfigureBall()
    {
        Renderer ballRenderer = FindRenderer("Basketball");
        if (ballRenderer == null)
        {
            return;
        }

        Material material = ballRenderer.material;
        material.SetFloat("_Smoothness", 0.62f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_EnvironmentReflections", 1f);

        if (material.HasProperty("_ClearCoatMask"))
        {
            material.EnableKeyword("_CLEARCOAT");
            material.SetFloat("_ClearCoatMask", 0.25f);
            material.SetFloat("_ClearCoatSmoothness", 0.8f);
        }

        Texture2D pebbleNormal = CreatePebbleNormalTexture(128);
        material.SetTexture("_BumpMap", pebbleNormal);
        material.SetFloat("_BumpScale", 0.55f);
        material.EnableKeyword("_NORMALMAP");

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTextureScale("_BumpMap", new Vector2(2f, 1f));
        }

        SetEmission(material, Color.black);
    }

    private void ConfigureRim()
    {
        Renderer rimRenderer = FindRenderer("Rim");
        if (rimRenderer == null)
        {
            return;
        }

        Material material = rimRenderer.material;
        material.SetColor("_BaseColor", new Color(0.95f, 0.26f, 0.05f, 1f));
        material.SetFloat("_Smoothness", 0.8f);
        material.SetFloat("_Metallic", 0.45f);
        SetEmission(material, new Color(0.28f, 0.05f, 0.01f, 1f));
    }

    private void ConfigureBackboard()
    {
        GameObject backboard = GameObject.Find("Backboard");
        if (backboard == null)
        {
            return;
        }

        Renderer boardRenderer = backboard.GetComponent<Renderer>();
        if (boardRenderer == null)
        {
            return;
        }

        Material glass = boardRenderer.material;
        glass.SetColor("_BaseColor", new Color(0.72f, 0.86f, 1f, 0.22f));
        glass.SetFloat("_Smoothness", 0.94f);
        glass.SetFloat("_Metallic", 0.05f);
        glass.SetFloat("_Surface", 1f);
        glass.SetFloat("_Blend", 0f);
        glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        glass.SetFloat("_ZWrite", 0f);
        glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        glass.SetOverrideTag("RenderType", "Transparent");
        glass.renderQueue = (int)RenderQueue.Transparent;

        boardRenderer.shadowCastingMode = ShadowCastingMode.Off;
        boardRenderer.receiveShadows = false;

        Bounds bounds = boardRenderer.bounds;
        Material frameMaterial = CreateLitMaterial("BackboardFrame", new Color(0.92f, 0.26f, 0.06f), 0.55f, 0.2f);
        Material targetMaterial = CreateLitMaterial("BackboardTarget", new Color(1f, 0.97f, 0.9f), 0.6f, 0f);
        SetEmission(targetMaterial, new Color(0.35f, 0.33f, 0.28f));

        float frameThickness = 0.07f;
        float frameDepth = bounds.size.z + 0.08f;

        CreateCube("BackboardFrameTop", transform, new Vector3(bounds.center.x, bounds.max.y + frameThickness * 0.5f, bounds.center.z), new Vector3(bounds.size.x + 0.2f, frameThickness, frameDepth), frameMaterial);
        CreateCube("BackboardFrameBottom", transform, new Vector3(bounds.center.x, bounds.min.y - frameThickness * 0.5f, bounds.center.z), new Vector3(bounds.size.x + 0.2f, frameThickness, frameDepth), frameMaterial);
        CreateCube("BackboardFrameLeft", transform, new Vector3(bounds.min.x - frameThickness * 0.5f, bounds.center.y, bounds.center.z), new Vector3(frameThickness, bounds.size.y + 0.2f, frameDepth), frameMaterial);
        CreateCube("BackboardFrameRight", transform, new Vector3(bounds.max.x + frameThickness * 0.5f, bounds.center.y, bounds.center.z), new Vector3(frameThickness, bounds.size.y + 0.2f, frameDepth), frameMaterial);

        float frontZ = bounds.min.z - 0.02f;
        float targetY = bounds.center.y + 0.02f;
        float targetWidth = 0.62f;
        float targetHeight = 0.46f;
        float bar = 0.03f;

        CreateCube("TargetTop", transform, new Vector3(bounds.center.x, targetY + targetHeight * 0.5f, frontZ), new Vector3(targetWidth + bar, bar, 0.02f), targetMaterial);
        CreateCube("TargetBottom", transform, new Vector3(bounds.center.x, targetY - targetHeight * 0.5f, frontZ), new Vector3(targetWidth + bar, bar, 0.02f), targetMaterial);
        CreateCube("TargetLeft", transform, new Vector3(bounds.center.x - targetWidth * 0.5f, targetY, frontZ), new Vector3(bar, targetHeight, 0.02f), targetMaterial);
        CreateCube("TargetRight", transform, new Vector3(bounds.center.x + targetWidth * 0.5f, targetY, frontZ), new Vector3(bar, targetHeight, 0.02f), targetMaterial);
    }

    private void ConfigureHoopPole()
    {
        Renderer poleRenderer = FindRenderer("Hoop Pole");
        if (poleRenderer == null)
        {
            return;
        }

        Material material = poleRenderer.material;
        material.SetColor("_BaseColor", new Color(0.08f, 0.09f, 0.11f, 1f));
        material.SetFloat("_Smoothness", 0.68f);
        material.SetFloat("_Metallic", 0.8f);
    }

    private void ConfigureNet()
    {
        BasketballNet net = FindAnyObjectByType<BasketballNet>();
        if (net == null)
        {
            return;
        }

        net.ropeCount = Mathf.Max(14, net.ropeCount);
        net.rows = Mathf.Max(6, net.rows);
        net.topRadius = 0.285f;
        net.bottomRadius = 0.15f;
        net.netHeight = 0.48f;
        net.ropeWidth = Mathf.Max(0.022f, net.ropeWidth);
        net.swayAngle = 7f;
        net.swayCycles = 2.5f;

        if (net.ropeMaterial != null)
        {
            Material rope = new Material(net.ropeMaterial);
            rope.SetColor("_BaseColor", new Color(0.95f, 0.95f, 0.9f, 1f));
            rope.SetFloat("_Smoothness", 0.7f);
            SetEmission(rope, new Color(0.1f, 0.1f, 0.09f));
            net.ropeMaterial = rope;
        }
    }

    private void ConfigureCeilingLights()
    {
        EnableEmissivePanel("CeilingPanelA", new Color(4.2f, 3.9f, 3.3f, 1f));
        EnableEmissivePanel("CeilingPanelB", new Color(4.2f, 3.9f, 3.3f, 1f));
    }

    private void EnableEmissivePanel(string objectName, Color emission)
    {
        Renderer renderer = FindRenderer(objectName);
        if (renderer == null)
        {
            return;
        }

        Material material = renderer.material;
        material.SetFloat("_Smoothness", 0.4f);
        SetEmission(material, emission);
    }

    private void ConfigureCourtLines()
    {
        Renderer lines = FindRenderer("CourtLineOverlay");
        if (lines == null)
        {
            return;
        }

        Material material = lines.material;
        material.SetFloat("_Smoothness", 0.32f);
        material.SetFloat("_Metallic", 0f);
    }

    private void ApplyPostProcessing()
    {
        GameObject volumeObject = new GameObject("ArenaPostFX");
        volumeObject.transform.SetParent(transform, false);

        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.weight = 1f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "ArenaPostFXProfile";
        volume.sharedProfile = profile;

        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.14f);
        color.contrast.Override(12f);
        color.saturation.Override(9f);
        color.colorFilter.Override(new Color(1f, 0.97f, 0.9f));

        WhiteBalance whiteBalance = profile.Add<WhiteBalance>(true);
        whiteBalance.temperature.Override(7f);
        whiteBalance.tint.Override(2f);

        if (!mobile)
        {
            FilmGrain grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.08f);
            grain.response.Override(0.8f);

            ChromaticAberration aberration = profile.Add<ChromaticAberration>(true);
            aberration.intensity.Override(0.04f);
        }
    }

    private void CreateArenaLights()
    {
        CreateSpotLight("ArenaKeySpot", new Vector3(0f, 6.8f, -4f), new Vector3(0f, 1.4f, 3.5f), new Color(1f, 0.9f, 0.78f), 5f, 24f, 58f);
        CreateSpotLight("HoopKeySpot", new Vector3(0f, 6.7f, 6.2f), new Vector3(0f, 3.05f, 4.5f), new Color(1f, 0.74f, 0.46f), 3.5f, 14f, 36f);

        GameObject rimLightObject = new GameObject("RimFlashLight");
        rimLightObject.transform.SetParent(transform, false);
        rimLightObject.transform.position = new Vector3(0f, 3.28f, 4.5f);
        rimFlashLight = rimLightObject.AddComponent<Light>();
        rimFlashLight.type = LightType.Point;
        rimFlashLight.color = new Color(1f, 0.62f, 0.28f);
        rimFlashLight.range = 4f;
        rimFlashLight.intensity = 0f;
        rimFlashLight.shadows = LightShadows.None;

        RenderSettings.ambientIntensity = 1.08f;

        if (hoopScore != null)
        {
            hoopScore.BasketScored += HandleBasketScored;
        }
    }

    private void CreateSpotLight(string name, Vector3 position, Vector3 target, Color color, float intensity, float range, float angle)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.position = position;
        lightObject.transform.LookAt(target);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = angle;
        light.innerSpotAngle = angle * 0.55f;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.Auto;
    }

    private void BuildStands()
    {
        Material standMaterial = CreateLitMaterial("ArenaStand", new Color(0.035f, 0.045f, 0.06f), 0.28f, 0.05f);
        Material crowdSideMaterial = CreateUnlitMaterial("CrowdSide", crowdTexture, Color.white);
        crowdSideMaterial.SetTextureScale("_BaseMap", new Vector2(11f, 1f));
        Material crowdBackMaterial = CreateUnlitMaterial("CrowdBack", crowdTexture, Color.white);
        crowdBackMaterial.SetTextureScale("_BaseMap", new Vector2(7.2f, 1f));

        GameObject standsRoot = new GameObject("ArenaStands");
        standsRoot.transform.SetParent(transform, false);

        for (int side = -1; side <= 1; side += 2)
        {
            for (int row = 0; row < 3; row++)
            {
                float distance = 8.6f + row * 0.52f;
                float height = 0.5f + row * 0.55f;
                float x = side * distance;

                CreateCube("StandStep", standsRoot.transform, new Vector3(x, height * 0.5f, 0f), new Vector3(0.58f, height, 28.6f), standMaterial);

                float crowdX = side * (distance - 0.28f);
                Quaternion rotation = Quaternion.Euler(0f, side < 0 ? -90f : 90f, 0f);

                CreateQuad("CrowdSide", standsRoot.transform, new Vector3(crowdX, height + 0.45f, 0f), rotation, new Vector2(28.4f, 0.95f), crowdSideMaterial);
            }
        }

        for (int row = 0; row < 3; row++)
        {
            float z = 13.2f + row * 0.52f;
            float height = 0.5f + row * 0.55f;

            CreateCube("BackStandStep", standsRoot.transform, new Vector3(0f, height * 0.5f, z), new Vector3(18.8f, height, 0.58f), standMaterial);
            CreateQuad("CrowdBack", standsRoot.transform, new Vector3(0f, height + 0.45f, z - 0.29f), Quaternion.identity, new Vector2(18.4f, 0.95f), crowdBackMaterial);
        }
    }

    private void BuildLedBoards()
    {
        ledMaterial = CreateLedMaterial();

        GameObject ledRoot = new GameObject("ArenaLedBoards");
        ledRoot.transform.SetParent(transform, false);

        CreateQuad("LedLeft", ledRoot.transform, new Vector3(-9.88f, 3.15f, 0f), Quaternion.Euler(0f, -90f, 0f), new Vector2(27f, 0.5f), ledMaterial);
        CreateQuad("LedRight", ledRoot.transform, new Vector3(9.88f, 3.15f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector2(27f, 0.5f), ledMaterial);
        CreateQuad("LedBack", ledRoot.transform, new Vector3(0f, 3.15f, 14.9f), Quaternion.identity, new Vector2(18f, 0.5f), ledMaterial);
    }

    private void BuildBanners()
    {
        Material bannerMaterial = CreateUnlitMaterial("ArenaBanner", bannerTexture, Color.white);

        GameObject bannerRoot = new GameObject("ArenaBanners");
        bannerRoot.transform.SetParent(transform, false);

        CreateQuad("BannerLeft", bannerRoot.transform, new Vector3(3.2f, 5.25f, 14.9f), Quaternion.identity, new Vector2(1.7f, 2.5f), bannerMaterial);
        CreateQuad("BannerRight", bannerRoot.transform, new Vector3(6.8f, 5.25f, 14.9f), Quaternion.identity, new Vector2(1.7f, 2.5f), bannerMaterial);
        CreateQuad("BannerSideLeft", bannerRoot.transform, new Vector3(-9.88f, 5.2f, 4f), Quaternion.Euler(0f, -90f, 0f), new Vector2(2.4f, 2.6f), bannerMaterial);
        CreateQuad("BannerSideRight", bannerRoot.transform, new Vector3(9.88f, 5.2f, 4f), Quaternion.Euler(0f, 90f, 0f), new Vector2(2.4f, 2.6f), bannerMaterial);

        Material rodMaterial = CreateLitMaterial("BannerRod", new Color(0.15f, 0.15f, 0.18f), 0.5f, 0.6f);
        CreateCube("BannerRodLeft", bannerRoot.transform, new Vector3(3.2f, 6.52f, 14.9f), new Vector3(2.1f, 0.06f, 0.06f), rodMaterial);
        CreateCube("BannerRodRight", bannerRoot.transform, new Vector3(6.8f, 6.52f, 14.9f), new Vector3(2.1f, 0.06f, 0.06f), rodMaterial);
    }

    private void BuildJumbotron()
    {
        Font font = Resources.Load<Font>("HUDFont");
        if (font == null)
        {
            font = Font.CreateDynamicFontFromOSFont("Arial", 48);
        }

        GameObject canvasObject = new GameObject("JumbotronCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.transform.localPosition = new Vector3(-6.2f, 5.2f, 14.88f);
        canvasObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        canvasObject.transform.localScale = Vector3.one * 0.01f;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 10;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(720f, 360f);

        GameObject background = new GameObject("Background", typeof(RectTransform));
        background.transform.SetParent(canvasObject.transform, false);
        RectTransform backgroundRect = background.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        Image backgroundImage = background.AddComponent<Image>();
        backgroundImage.color = new Color(0.015f, 0.02f, 0.03f, 0.94f);
        backgroundImage.raycastTarget = false;

        Color accent = new Color(1f, 0.5f, 0.12f, 1f);
        CreateJumboBorder(canvasObject.transform, "BorderTop", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 8f), accent);
        CreateJumboBorder(canvasObject.transform, "BorderBottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), accent);
        CreateJumboBorder(canvasObject.transform, "BorderLeft", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), accent);
        CreateJumboBorder(canvasObject.transform, "BorderRight", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(8f, 0f), accent);

        CreateJumboImage(canvasObject.transform, "Divider", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(2f, 150f), new Color(1f, 1f, 1f, 0.16f));

        CreateJumboText(canvasObject.transform, "Header", "RUSH ARENA", font, 30, new Vector2(0f, 148f), new Vector2(680f, 40f), new Color(1f, 0.82f, 0.3f));
        CreateJumboText(canvasObject.transform, "ScoreLabel", "SCORE", font, 20, new Vector2(-180f, 86f), new Vector2(300f, 32f), new Color(0.7f, 0.73f, 0.78f));
        CreateJumboText(canvasObject.transform, "TimeLabel", "TIME", font, 20, new Vector2(180f, 86f), new Vector2(300f, 32f), new Color(0.7f, 0.73f, 0.78f));

        scoreText = CreateJumboText(canvasObject.transform, "ScoreValue", "0", font, 64, new Vector2(-180f, 15f), new Vector2(300f, 110f), Color.white);
        timeText = CreateJumboText(canvasObject.transform, "TimeValue", "60", font, 58, new Vector2(180f, 15f), new Vector2(300f, 110f), Color.white);
        bestText = CreateJumboText(canvasObject.transform, "BestValue", "BEST 0", font, 22, new Vector2(0f, -105f), new Vector2(680f, 34f), new Color(1f, 0.82f, 0.3f));
        statusText = CreateJumboText(canvasObject.transform, "StatusValue", "GET READY", font, 20, new Vector2(0f, -145f), new Vector2(680f, 32f), new Color(0.7f, 0.9f, 1f));
    }

    private void CreateJumboBorder(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    private Image CreateJumboImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Text CreateJumboText(Transform parent, string name, string value, Font font, int size, Vector2 position, Vector2 sizeDelta, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;

        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.text = value;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        return text;
    }

    private void BuildCourtLogo()
    {
        Material logoMaterial = CreateSpriteMaterial("CourtLogo", emblemTexture, new Color(1f, 1f, 1f, 0.5f));

        GameObject logo = CreateQuad("CourtLogo", transform, new Vector3(0f, 0.022f, 0.4f), Quaternion.Euler(90f, 0f, 0f), new Vector2(3.4f, 3.4f), logoMaterial);
        logo.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    private void BuildDust()
    {
        GameObject dustObject = new GameObject("ArenaDust");
        dustObject.transform.SetParent(transform, false);
        dustObject.transform.position = new Vector3(0f, 3.2f, 3.5f);

        ParticleSystem particles = dustObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 6f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 12f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.06f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.04f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.75f, 0.16f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = mobile ? 24 : 50;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = mobile ? 1.5f : 3f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(17f, 4f, 24f);

        ParticleSystemRenderer renderer = dustObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.material = CreateSpriteMaterial("ArenaDustMaterial", softCircleTexture, Color.white);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        particles.Play(true);
    }

    private void CreateReflectionProbe()
    {
        GameObject probeObject = new GameObject("ArenaReflectionProbe");
        probeObject.transform.SetParent(transform, false);
        probeObject.transform.position = new Vector3(0f, 1.6f, 0f);

        ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Custom;
        probe.size = new Vector3(19f, 6.4f, 28f);
        probe.center = Vector3.zero;
        probe.resolution = mobile ? 64 : 128;
        probe.hdr = true;
        probe.boxProjection = true;
        probe.importance = 1;
        probe.blendDistance = 1.5f;
        probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
        probe.backgroundColor = new Color(0.02f, 0.025f, 0.04f, 1f);
        probe.cullingMask = ~0;
        probe.RenderProbe();
    }

    private void RefreshScoreboard()
    {
        if (Time.unscaledTime < nextScoreboardRefresh)
        {
            return;
        }

        nextScoreboardRefresh = Time.unscaledTime + 0.2f;

        if (hud == null)
        {
            hud = GameHUD.Instance;
        }

        int score = hoopScore != null ? hoopScore.score : 0;
        int best = hud != null ? hud.BestScoreValue : 0;
        int time = hud != null ? hud.DisplayedTimeSeconds : 60;
        bool ready = hud == null || !hud.HasGameStarted || hud.IsCountdownActive;
        bool finished = hud != null && hud.IsTimerFinished;
        int status = finished ? 2 : (ready ? 0 : 1);

        if (scoreText != null && score != lastScore)
        {
            lastScore = score;
            scoreText.text = score.ToString();
        }

        if (timeText != null && time != lastTime)
        {
            lastTime = time;
            timeText.text = time.ToString();
        }

        if (bestText != null && best != lastBest)
        {
            lastBest = best;
            bestText.text = "BEST " + best;
        }

        if (statusText != null && status != lastStatus)
        {
            lastStatus = status;
            statusText.text = finished ? "GAME OVER" : (ready ? "GET READY" : "LIVE");
            statusText.color = finished
                ? new Color(1f, 0.45f, 0.35f)
                : (ready ? new Color(0.7f, 0.9f, 1f) : new Color(0.5f, 1f, 0.65f));
        }
    }

    private Renderer FindRenderer(string objectName)
    {
        GameObject target = GameObject.Find(objectName);
        return target != null ? target.GetComponent<Renderer>() : null;
    }

    private GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 size, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localRotation = Quaternion.identity;
        cube.transform.localScale = size;

        Collider collider = cube.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        Renderer renderer = cube.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return cube;
    }

    private GameObject CreateQuad(string name, Transform parent, Vector3 position, Quaternion rotation, Vector2 size, Material material)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = position;
        quad.transform.localRotation = rotation;
        quad.transform.localScale = new Vector3(size.x, size.y, 1f);

        Collider collider = quad.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return quad;
    }

    private static Material CreateLitMaterial(string name, Color color, float smoothness, float metallic)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            name = name,
            hideFlags = HideFlags.DontSave
        };

        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        return material;
    }

    private static Material CreateUnlitMaterial(string name, Texture2D texture, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Texture");
        }

        Material material = new Material(shader)
        {
            name = name,
            hideFlags = HideFlags.DontSave
        };

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", color);
        }
        else
        {
            material.mainTexture = texture;
            material.color = color;
        }

        return material;
    }

    private static Material CreateSpriteMaterial(string name, Texture2D texture, Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        Material material = new Material(shader)
        {
            name = name,
            hideFlags = HideFlags.DontSave
        };

        material.mainTexture = texture;
        material.color = color;
        return material;
    }

    private Material CreateLedMaterial()
    {
        Material material = CreateLitMaterial("ArenaLED", new Color(0.02f, 0.02f, 0.03f), 0.55f, 0f);
        material.SetTexture("_BaseMap", ledTexture);
        material.SetTextureScale("_BaseMap", new Vector2(10f, 1f));

        if (material.HasProperty("_EmissionMap"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", ledTexture);
            material.SetTextureScale("_EmissionMap", new Vector2(10f, 1f));
            material.SetColor("_EmissionColor", new Color(1.9f, 0.95f, 0.35f, 1f));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        return material;
    }

    private static void SetEmission(Material material, Color color)
    {
        if (!material.HasProperty("_EmissionColor"))
        {
            return;
        }

        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    private static Texture2D CreateCrowdTexture()
    {
        const int width = 256;
        const int height = 128;
        Texture2D texture = NewTexture(width, height, false);

        Color32 background = new Color32(10, 13, 20, 255);
        Color32[] pixels = new Color32[width * height];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = background;
        }

        Color32[] palette =
        {
            new Color32(245, 120, 32, 255),
            new Color32(235, 235, 240, 255),
            new Color32(70, 120, 205, 255),
            new Color32(205, 62, 52, 255),
            new Color32(255, 196, 70, 255),
            new Color32(76, 190, 165, 255)
        };

        System.Random random = new System.Random(9182);

        for (int row = 0; row < 8; row++)
        {
            int baseY = 8 + row * 14;

            for (int column = 0; column < 34; column++)
            {
                if (column % 9 == 8)
                {
                    continue;
                }

                int x = 4 + column * 7 + random.Next(0, 3);
                int y = baseY + random.Next(0, 3);
                Color32 body = palette[random.Next(palette.Length)];
                float shade = 0.55f + (float)random.NextDouble() * 0.6f;
                body = new Color32(
                    (byte)Mathf.Clamp(body.r * shade, 0f, 255f),
                    (byte)Mathf.Clamp(body.g * shade, 0f, 255f),
                    (byte)Mathf.Clamp(body.b * shade, 0f, 255f),
                    255);

                DrawRect(pixels, width, height, x, y, 4, 6, body);

                Color32 head = new Color32(
                    (byte)Mathf.Clamp(body.r * 1.15f, 0f, 255f),
                    (byte)Mathf.Clamp(body.g * 1.15f, 0f, 255f),
                    (byte)Mathf.Clamp(body.b * 1.15f, 0f, 255f),
                    255);

                DrawRect(pixels, width, height, x, y + 6, 3, 3, head);
            }
        }

        for (int x = 0; x < width; x += 64)
        {
            DrawRect(pixels, width, height, x, 0, 2, height, new Color32(4, 6, 10, 255));
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateBannerTexture()
    {
        const int width = 128;
        const int height = 256;
        Texture2D texture = NewTexture(width, height, false);

        Color32[] pixels = new Color32[width * height];
        Color32 background = new Color32(14, 19, 32, 255);
        Color32 border = new Color32(255, 148, 38, 255);
        Color32 emblem = new Color32(238, 108, 26, 255);
        Color32 white = new Color32(240, 240, 245, 255);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color32 color = background;

                if (x < 5 || x >= width - 5 || y < 5 || y >= height - 5)
                {
                    color = border;
                }

                float dx = x - width * 0.5f;
                float dy = y - height * 0.72f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                if (distance < 30f)
                {
                    color = emblem;
                }
                else if (distance < 35f)
                {
                    color = white;
                }

                int stripe = (int)((x + y * 0.6f) / 14f) % 2;
                if (y < 70 && y > 18 && stripe == 0 && x > 12 && x < width - 12)
                {
                    color = new Color32(235, 110, 28, 255);
                }

                pixels[y * width + x] = color;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateLedTexture()
    {
        const int width = 256;
        const int height = 32;
        Texture2D texture = NewTexture(width, height, false);

        Color32[] pixels = new Color32[width * height];
        Color32 orange = new Color32(255, 112, 18, 255);
        Color32 warmWhite = new Color32(255, 238, 205, 255);
        Color32 dark = new Color32(18, 12, 6, 255);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool border = y < 2 || y >= height - 2;
                int band = (x + y * 2) / 12;

                Color32 color = border
                    ? dark
                    : (band % 2 == 0 ? orange : warmWhite);

                if (!border && x % 48 < 4)
                {
                    color = dark;
                }

                pixels[y * width + x] = color;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateEmblemTexture()
    {
        const int size = 256;
        Texture2D texture = NewTexture(size, size, false);

        Color32[] pixels = new Color32[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - half;
                float dy = y + 0.5f - half;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                if (distance > 124f)
                {
                    pixels[y * size + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                Color32 color = new Color32(232, 118, 28, 235);

                if (distance > 115f)
                {
                    color = new Color32(245, 240, 230, 245);
                }
                else
                {
                    bool seam = Mathf.Abs(dx) < 2.5f || Mathf.Abs(dy) < 2.5f;

                    float rightArc = Mathf.Abs(Mathf.Sqrt((dx - 96f) * (dx - 96f) + dy * dy) - 132f);
                    float leftArc = Mathf.Abs(Mathf.Sqrt((dx + 96f) * (dx + 96f) + dy * dy) - 132f);
                    seam |= rightArc < 2.8f || leftArc < 2.8f;

                    if (seam && distance < 112f)
                    {
                        color = new Color32(35, 18, 10, 245);
                    }
                    else if (distance > 104f)
                    {
                        color = new Color32(150, 68, 16, 235);
                    }
                }

                pixels[y * size + x] = color;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateSoftCircleTexture()
    {
        const int size = 32;
        Texture2D texture = NewTexture(size, size, false);

        Color32[] pixels = new Color32[size * size];
        float half = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - distance);
                alpha *= alpha;

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Texture2D CreateWoodNormalTexture(int size)
    {
        return CreateNormalTexture(size, (x, y) =>
        {
            float px = x * 0.09f;
            float py = y * 0.04f;
            return Mathf.Sin(px * 2f + Mathf.Sin(py * 3f) * 2.2f) * 0.35f +
                   Mathf.Sin(px * 5.5f + py * 1.3f) * 0.18f +
                   Mathf.PerlinNoise(px * 0.8f, py * 2f) * 0.25f;
        });
    }

    private static Texture2D CreatePebbleNormalTexture(int size)
    {
        System.Random random = new System.Random(4471);
        const int pebbleCount = 260;
        Vector3[] pebbles = new Vector3[pebbleCount];

        for (int i = 0; i < pebbleCount; i++)
        {
            pebbles[i] = new Vector3(
                (float)random.NextDouble() * size,
                (float)random.NextDouble() * size,
                1.5f + (float)random.NextDouble() * 2.2f);
        }

        return CreateNormalTexture(size, (x, y) =>
        {
            float height = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.12f;

            for (int i = 0; i < pebbleCount; i++)
            {
                float dx = x - pebbles[i].x;
                float dy = y - pebbles[i].y;

                if (dx > size * 0.5f) dx -= size;
                if (dx < -size * 0.5f) dx += size;
                if (dy > size * 0.5f) dy -= size;
                if (dy < -size * 0.5f) dy += size;

                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float radius = pebbles[i].z;

                if (distance < radius)
                {
                    float bump = Mathf.Sqrt(1f - (distance * distance) / (radius * radius));
                    height = Mathf.Max(height, bump * 0.45f);
                }
            }

            return height;
        });
    }

    private static Texture2D CreateNormalTexture(int size, Func<float, float, float> heightFunction)
    {
        Texture2D texture = NewTexture(size, size, true);
        Color32[] pixels = new Color32[size * size];
        const float strength = 2.2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float left = heightFunction(x - 1, y);
                float right = heightFunction(x + 1, y);
                float down = heightFunction(x, y - 1);
                float up = heightFunction(x, y + 1);

                Vector3 normal = new Vector3(
                    (left - right) * strength,
                    (down - up) * strength,
                    1f).normalized;

                pixels[y * size + x] = new Color32(
                    (byte)Mathf.RoundToInt((normal.x * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((normal.y * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((normal.z * 0.5f + 0.5f) * 255f),
                    255);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(true, true);
        return texture;
    }

    private static Texture2D NewTexture(int width, int height, bool linear)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, true, linear)
        {
            hideFlags = HideFlags.DontSave,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 4
        };

        return texture;
    }

    private static void DrawRect(Color32[] pixels, int width, int height, int startX, int startY, int rectWidth, int rectHeight, Color32 color)
    {
        for (int y = startY; y < startY + rectHeight; y++)
        {
            if (y < 0 || y >= height)
            {
                continue;
            }

            for (int x = startX; x < startX + rectWidth; x++)
            {
                if (x < 0 || x >= width)
                {
                    continue;
                }

                pixels[y * width + x] = color;
            }
        }
    }
}
