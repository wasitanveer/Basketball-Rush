using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class ScoreParticleBurst : MonoBehaviour
{
    private const int ParticleTextureSize = 32;
    private const int BurstCountMin = 8;
    private const int BurstCountMax = 13;

    private static Material sharedParticleMaterial;

    private ParticleSystem burst;

    private void Awake()
    {
        EnsureBurst();
    }

    public void PlayBurst()
    {
        EnsureBurst();

        burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        burst.Play(true);
    }

    public void StopAndClear()
    {
        if (burst == null)
        {
            return;
        }

        burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void EnsureBurst()
    {
        if (burst == null)
        {
            CreateBurst();
        }
    }

    private void CreateBurst()
    {
        burst = gameObject.AddComponent<ParticleSystem>();
        burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = burst.main;
        main.loop = false;
        main.playOnAwake = false;
        main.prewarm = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.8f, 0.3f, 1f),
            new Color(1f, 0.5f, 0.12f, 1f));
        main.gravityModifier = 0.7f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 30;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = burst.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)BurstCountMin, (short)BurstCountMax)
        });

        ParticleSystem.ShapeModule shape = burst.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 70f;
        shape.radius = 0.1f;
        shape.radiusThickness = 0.2f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = burst.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(BuildFadeGradient());

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = burst.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.7f, 0.75f),
                new Keyframe(1f, 0f)));

        ParticleSystemRenderer particleRenderer = GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.material = GetParticleMaterial();
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
    }

    private static Gradient BuildFadeGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.35f),
                new GradientAlphaKey(0f, 1f)
            });

        return gradient;
    }

    private static Material GetParticleMaterial()
    {
        if (sharedParticleMaterial != null)
        {
            return sharedParticleMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        if (shader == null)
        {
            Debug.LogWarning("[ScoreParticleBurst] No compatible shader was found for the score particles.");
            return null;
        }

        sharedParticleMaterial = new Material(shader)
        {
            name = "ScoreParticleMaterial",
            hideFlags = HideFlags.DontSave,
            mainTexture = CreateSoftCircleTexture()
        };

        return sharedParticleMaterial;
    }

    private static Texture2D CreateSoftCircleTexture()
    {
        Texture2D texture = new Texture2D(ParticleTextureSize, ParticleTextureSize, TextureFormat.RGBA32, false);
        texture.name = "ScoreParticleCircle";
        texture.hideFlags = HideFlags.DontSave;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[ParticleTextureSize * ParticleTextureSize];
        float half = (ParticleTextureSize - 1) * 0.5f;

        for (int y = 0; y < ParticleTextureSize; y++)
        {
            for (int x = 0; x < ParticleTextureSize; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - distance);
                alpha *= alpha;

                pixels[y * ParticleTextureSize + x] = new Color32(
                    255,
                    255,
                    255,
                    (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        return texture;
    }
}
