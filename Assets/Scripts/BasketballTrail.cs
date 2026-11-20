using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class BasketballTrail : MonoBehaviour
{
    private const float TrailLifetime = 0.16f;
    private const float TrailStartWidth = 0.055f;
    private const float TrailMinVertexDistance = 0.02f;

    private static Material sharedTrailMaterial;

    private TrailRenderer trail;
    private bool trailActive;

    private void Awake()
    {
        EnsureTrail();
    }

    public void SetTrailActive(bool active)
    {
        EnsureTrail();

        if (trailActive == active)
        {
            return;
        }

        trailActive = active;
        trail.enabled = active;
        trail.emitting = active;
        trail.Clear();
    }

    public void ClearTrail()
    {
        EnsureTrail();
        trailActive = false;
        trail.enabled = false;
        trail.emitting = false;
        trail.Clear();
    }

    private void EnsureTrail()
    {
        if (trail == null)
        {
            CreateTrail();
        }
    }

    private void CreateTrail()
    {
        GameObject trailObject = new GameObject("BallTrail");
        trailObject.transform.SetParent(transform, false);

        Vector3 parentScale = transform.lossyScale;
        trailObject.transform.localScale = new Vector3(
            InverseScale(parentScale.x),
            InverseScale(parentScale.y),
            InverseScale(parentScale.z));

        trail = trailObject.AddComponent<TrailRenderer>();

        trail.time = TrailLifetime;
        trail.minVertexDistance = TrailMinVertexDistance;
        trail.widthMultiplier = 1f;
        trail.widthCurve = new AnimationCurve(
            new Keyframe(0f, TrailStartWidth),
            new Keyframe(0.45f, TrailStartWidth * 0.62f),
            new Keyframe(1f, 0f));
        trail.colorGradient = BuildTrailGradient();
        trail.numCapVertices = 2;
        trail.numCornerVertices = 0;
        trail.textureMode = LineTextureMode.Stretch;
        trail.alignment = LineAlignment.View;
        trail.autodestruct = false;
        trail.emitting = false;
        trail.enabled = false;
        trail.generateLightingData = false;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.material = GetTrailMaterial();
        trail.Clear();
    }

    private static float InverseScale(float value)
    {
        return Mathf.Abs(value) < 0.0001f ? 1f : 1f / value;
    }

    private static Gradient BuildTrailGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.76f, 0.4f), 0f),
                new GradientColorKey(new Color(1f, 0.56f, 0.18f), 0.35f),
                new GradientColorKey(new Color(1f, 0.44f, 0.1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.7f, 0f),
                new GradientAlphaKey(0.38f, 0.35f),
                new GradientAlphaKey(0f, 1f)
            });

        return gradient;
    }

    private static Material GetTrailMaterial()
    {
        if (sharedTrailMaterial != null)
        {
            return sharedTrailMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        }

        if (shader == null)
        {
            Debug.LogWarning("[BasketballTrail] No compatible shader was found for the ball trail.");
            return null;
        }

        sharedTrailMaterial = new Material(shader)
        {
            name = "BasketballTrailMaterial",
            hideFlags = HideFlags.DontSave,
            mainTexture = Texture2D.whiteTexture
        };

        return sharedTrailMaterial;
    }
}
