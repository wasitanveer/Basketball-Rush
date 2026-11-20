using UnityEngine;
using UnityEngine.Rendering;

public class BasketballNet : MonoBehaviour
{
    public Material ropeMaterial;
    public int ropeCount = 12;
    public int rows = 5;
    public float topRadius = 0.28f;
    public float bottomRadius = 0.16f;
    public float netHeight = 0.45f;
    public float ropeWidth = 0.015f;

    [Header("Sway")]
    public float swayAngle = 5f;
    public float swayDuration = 0.65f;
    public float swayCycles = 2f;

    private Transform netRoot;
    private float swayTimer;
    private float swayDirection = 1f;

    void Start()
    {
        // Remove a net that was built before, so we never get two nets.
        Transform oldNet = transform.Find("Net");
        if (oldNet != null)
        {
            Destroy(oldNet.gameObject);
        }

        BuildNet();
    }

    void Update()
    {
        if (netRoot == null || swayTimer <= 0f)
        {
            return;
        }

        swayTimer -= Time.deltaTime;

        float progress = 1f - Mathf.Clamp01(swayTimer / Mathf.Max(0.01f, swayDuration));
        float damping = 1f - progress;
        float angle = Mathf.Sin(progress * Mathf.PI * 2f * Mathf.Max(1f, swayCycles)) *
            swayAngle * damping * swayDirection;

        netRoot.localRotation = Quaternion.Euler(angle, 0f, angle * 0.35f);

        if (swayTimer <= 0f)
        {
            netRoot.localRotation = Quaternion.identity;
        }
    }

    public void Kick()
    {
        swayTimer = Mathf.Max(0.01f, swayDuration);
        swayDirection = Random.value < 0.5f ? -1f : 1f;
    }

    void BuildNet()
    {
        ropeCount = Mathf.Max(3, ropeCount);
        rows = Mathf.Max(1, rows);

        // Every rope piece is a child of this "Net" object.
        GameObject net = new GameObject("Net");
        net.transform.SetParent(transform, false);
        netRoot = net.transform;

        CreateRings(net.transform);
        CreateStrands(net.transform);
    }

    // Horizontal rope circles that connect the strands.
    void CreateRings(Transform net)
    {
        for (int row = 0; row <= rows; row++)
        {
            Vector3[] points = new Vector3[ropeCount];

            for (int i = 0; i < ropeCount; i++)
            {
                points[i] = GetPoint(row, i);
            }

            CreateRope(net, "NetRing", points, true);
        }
    }

    // Diagonal strands that cross each other and form the diamond pattern.
    void CreateStrands(Transform net)
    {
        for (int start = 0; start < ropeCount; start++)
        {
            CreateRope(net, "NetRope", BuildStrand(start, 1), false);
            CreateRope(net, "NetRope", BuildStrand(start, -1), false);
        }
    }

    Vector3[] BuildStrand(int start, int direction)
    {
        Vector3[] points = new Vector3[rows + 1];

        for (int row = 0; row <= rows; row++)
        {
            int column = (start + row * direction) % ropeCount;
            if (column < 0)
            {
                column += ropeCount;
            }

            points[row] = GetPoint(row, column);
        }

        return points;
    }

    // A point on the net. The top is wide and the bottom is narrow.
    Vector3 GetPoint(int row, int column)
    {
        float t = (float)row / rows;
        float radius = Mathf.Lerp(topRadius, bottomRadius, t);
        float angle = column * Mathf.PI * 2f / ropeCount;

        return new Vector3(
            Mathf.Cos(angle) * radius,
            -netHeight * t,
            Mathf.Sin(angle) * radius
        );
    }

    void CreateRope(Transform net, string ropeName, Vector3[] points, bool loop)
    {
        GameObject rope = new GameObject(ropeName);
        rope.transform.SetParent(net, false);

        LineRenderer line = rope.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = loop;
        line.positionCount = points.Length;
        line.SetPositions(points);
        line.startWidth = ropeWidth;
        line.endWidth = ropeWidth;
        line.numCapVertices = 2;
        line.material = ropeMaterial;
        line.shadowCastingMode = ShadowCastingMode.Off;
    }
}
