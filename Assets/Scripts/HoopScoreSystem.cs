using System;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class HoopScoreSystem : MonoBehaviour
{
    [Header("Ball")]
    [Tooltip("The shooting controller on the basketball. If empty, it is found automatically.")]
    public BallThrowController ballController;

    [Header("Net")]
    [Tooltip("Net that sways when a basket is scored. If empty, it is found automatically.")]
    public BasketballNet net;

    [Header("Rim")]
    [Tooltip("Radius of the rim circle in world units.")]
    public float rimRadius = 0.275f;

    [Tooltip("Thickness of the rim tube in world units.")]
    public float rimThickness = 0.03f;

    [Header("Score Trigger")]
    [Tooltip("How far the ball center may be from the rim center, horizontally, to count.")]
    public float scoreRadius = 0.16f;

    [Tooltip("Height of the trigger volume below the rim.")]
    public float triggerHeight = 0.5f;

    [Tooltip("How far below the rim the trigger center is placed.")]
    public float triggerOffsetY = 0.30f;

    [Tooltip("Minimum downward speed for a valid basket.")]
    public float minDownwardSpeed = 0.5f;

    public int score;

    public int Streak { get; private set; }

    public int LastPoints { get; private set; }

    public bool LastWasSwish { get; private set; }

    public event Action<int, bool, int> BasketScored;

    public event Action ShotMissed;

    public void ResetScore()
    {
        score = 0;
        Streak = 0;
        LastPoints = 0;
        LastWasSwish = false;
        scoredThisShot = false;

        if (scoreBurst != null)
        {
            scoreBurst.StopAndClear();
        }
    }

    private Rigidbody ballRigidbody;
    private bool scoredThisShot;
    private ScoreParticleBurst scoreBurst;
    private Vector3 lastBallPosition;
    private bool hasLastBallPosition;

    private void Awake()
    {
        if (ballController == null)
        {
            ballController = FindAnyObjectByType<BallThrowController>();
        }

        if (ballController != null)
        {
            ballRigidbody = ballController.GetComponent<Rigidbody>();
            ballController.ShotEnded += HandleShotEnded;
        }

        if (net == null)
        {
            net = FindAnyObjectByType<BasketballNet>();
        }

        BuildRimRing();
        BuildScoreTrigger();
        SetupScoreBurst();
    }

    private void OnDestroy()
    {
        if (ballController != null)
        {
            ballController.ShotEnded -= HandleShotEnded;
        }
    }

    private void HandleShotEnded(bool scored)
    {
        if (scored)
        {
            return;
        }

        Streak = 0;
        LastPoints = 0;
        LastWasSwish = false;
        ShotMissed?.Invoke();
    }

    private void SetupScoreBurst()
    {
        scoreBurst = GetComponentInChildren<ScoreParticleBurst>();
        if (scoreBurst != null)
        {
            return;
        }

        GameObject burstObject = new GameObject("ScoreParticleBurst");
        burstObject.transform.SetParent(transform, false);
        burstObject.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        scoreBurst = burstObject.AddComponent<ScoreParticleBurst>();
    }

    private void Update()
    {
        if (ballController != null && !ballController.IsInFlight)
        {
            scoredThisShot = false;
        }
    }

    private void FixedUpdate()
    {
        if (ballRigidbody == null || ballController == null)
        {
            return;
        }

        Vector3 currentPosition = ballRigidbody.position;

        if (!hasLastBallPosition || ballRigidbody.isKinematic || !ballController.IsInFlight)
        {
            lastBallPosition = currentPosition;
            hasLastBallPosition = true;
            return;
        }

        if (!scoredThisShot)
        {
            TryScoreCrossing(lastBallPosition, currentPosition);

            if (scoredThisShot)
            {
                lastBallPosition = ballRigidbody.position;
                return;
            }
        }

        lastBallPosition = currentPosition;
    }

    private void TryScoreCrossing(Vector3 previousPosition, Vector3 currentPosition)
    {
        if (ballRigidbody.linearVelocity.y > -minDownwardSpeed)
        {
            return;
        }

        float rimHeight = transform.position.y;
        if (previousPosition.y < rimHeight || currentPosition.y >= rimHeight)
        {
            return;
        }

        float denominator = previousPosition.y - currentPosition.y;
        if (denominator <= 0.0001f)
        {
            return;
        }

        float t = Mathf.Clamp01((previousPosition.y - rimHeight) / denominator);
        Vector3 crossingPoint = Vector3.Lerp(previousPosition, currentPosition, t);

        Vector3 flatOffset = new Vector3(
            crossingPoint.x - transform.position.x,
            0f,
            crossingPoint.z - transform.position.z);

        if (flatOffset.magnitude <= scoreRadius)
        {
            ScoreBasket();
        }
    }

    private void BuildRimRing()
    {
        CapsuleCollider oldRimCollider = GetComponent<CapsuleCollider>();
        if (oldRimCollider != null)
        {
            oldRimCollider.enabled = false;
        }

        transform.localScale = Vector3.one;

        Mesh torusMesh = CreateTorusMesh(rimRadius, rimThickness, 32, 10);

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            meshFilter.sharedMesh = torusMesh;
        }

        MeshCollider rimCollider = GetComponent<MeshCollider>();
        if (rimCollider == null)
        {
            rimCollider = gameObject.AddComponent<MeshCollider>();
        }

        rimCollider.convex = false;
        rimCollider.sharedMesh = torusMesh;
    }

    private void BuildScoreTrigger()
    {
        BoxCollider trigger = GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<BoxCollider>();
        }

        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, -triggerOffsetY, 0f);
        trigger.size = new Vector3(scoreRadius * 2f, triggerHeight, scoreRadius * 2f);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryScore(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryScore(other);
    }

    private void TryScore(Collider other)
    {
        if (scoredThisShot || ballController == null || ballRigidbody == null)
        {
            return;
        }

        if (other.attachedRigidbody != ballRigidbody)
        {
            return;
        }

        if (ballRigidbody.isKinematic || !ballController.IsInFlight)
        {
            return;
        }

        if (ballRigidbody.linearVelocity.y > -minDownwardSpeed)
        {
            return;
        }

        Vector3 ballPosition = ballRigidbody.position;
        if (ballPosition.y >= transform.position.y)
        {
            return;
        }

        Vector3 flatOffset = new Vector3(
            ballPosition.x - transform.position.x,
            0f,
            ballPosition.z - transform.position.z);

        if (flatOffset.magnitude > scoreRadius)
        {
            return;
        }

        ScoreBasket();
    }

    private void ScoreBasket()
    {
        scoredThisShot = true;
        Streak++;

        int comboBonus = Mathf.Min(4, Streak / 3);
        LastPoints = 1 + comboBonus;
        LastWasSwish = ballController != null && !ballController.HadRimOrBackboardContactThisShot;
        score += LastPoints;

        if (net != null)
        {
            net.Kick();
        }

        if (scoreBurst != null)
        {
            scoreBurst.PlayBurst();
        }

        if (LastWasSwish)
        {
            Haptics.Heavy();
        }
        else
        {
            Haptics.Medium();
        }

        if (ballController != null)
        {
            ballController.OnBasketScored();
        }

        BasketScored?.Invoke(LastPoints, LastWasSwish, Streak);
    }

    private static Mesh CreateTorusMesh(float majorRadius, float minorRadius, int majorSegments, int minorSegments)
    {
        Mesh mesh = new Mesh();
        mesh.name = "RimTorus";

        Vector3[] vertices = new Vector3[(majorSegments + 1) * (minorSegments + 1)];
        Vector3[] normals = new Vector3[vertices.Length];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[majorSegments * minorSegments * 6];

        int vertexIndex = 0;
        for (int i = 0; i <= majorSegments; i++)
        {
            float majorAngle = (float)i / majorSegments * Mathf.PI * 2f;
            float majorCos = Mathf.Cos(majorAngle);
            float majorSin = Mathf.Sin(majorAngle);

            for (int j = 0; j <= minorSegments; j++)
            {
                float minorAngle = (float)j / minorSegments * Mathf.PI * 2f;
                float minorCos = Mathf.Cos(minorAngle);
                float minorSin = Mathf.Sin(minorAngle);

                Vector3 normal = new Vector3(majorCos * minorCos, minorSin, majorSin * minorCos);
                Vector3 ringCenter = new Vector3(majorCos * majorRadius, 0f, majorSin * majorRadius);

                vertices[vertexIndex] = ringCenter + normal * minorRadius;
                normals[vertexIndex] = normal;
                uvs[vertexIndex] = new Vector2((float)i / majorSegments, (float)j / minorSegments);
                vertexIndex++;
            }
        }

        int triangleIndex = 0;
        for (int i = 0; i < majorSegments; i++)
        {
            for (int j = 0; j < minorSegments; j++)
            {
                int a = i * (minorSegments + 1) + j;
                int b = a + minorSegments + 1;

                triangles[triangleIndex++] = a;
                triangles[triangleIndex++] = a + 1;
                triangles[triangleIndex++] = b;

                triangles[triangleIndex++] = a + 1;
                triangles[triangleIndex++] = b + 1;
                triangles[triangleIndex++] = b;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }
}
