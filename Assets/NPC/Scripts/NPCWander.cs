using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class NPCWander : MonoBehaviour
{
    public NPCWaypointGroup waypointGroup;
    public float moveSpeed = 1.4f;
    public float rotationSpeed = 180f;
    public float stoppingDistance = 0.4f;
    public float waitTimeAtWaypoint = 1.5f;
    public float gravity = -18f;
    public float stuckTimeout = 1.8f;

    CharacterController controller;
    NPCWaypoint currentWaypoint;
    NPCWaypoint previousWaypoint;
    Vector3 targetPosition;
    Vector3 lastStuckPosition;
    float verticalVelocity;
    float waitTimer;
    float stuckTimer;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        lastStuckPosition = transform.position;
    }

    void Start()
    {
        if (waypointGroup == null) waypointGroup = FindAnyObjectByType<NPCWaypointGroup>();
        ConfigureCollisionIgnores();
        PickNextWaypoint();
    }

    void Update()
    {
        if (controller == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * dt;

        if (currentWaypoint == null)
        {
            if (waitTimer > 0f)
            {
                waitTimer -= dt;
                MoveWithGravity(Vector3.zero, dt);
                return;
            }

            PickNextWaypoint();
            if (currentWaypoint == null)
            {
                MoveWithGravity(Vector3.zero, dt);
                return;
            }
        }

        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (distance <= stoppingDistance)
        {
            previousWaypoint = currentWaypoint;
            currentWaypoint = null;
            waitTimer = waitTimeAtWaypoint;
            stuckTimer = 0f;
            MoveWithGravity(Vector3.zero, dt);
            return;
        }

        Vector3 direction = toTarget / distance;
        Quaternion desiredRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, rotationSpeed * dt);

        MoveWithGravity(direction * moveSpeed, dt);
        TrackStuck(dt);
    }

    void MoveWithGravity(Vector3 horizontal, float dt)
    {
        Vector3 motion = horizontal;
        motion.y = verticalVelocity;
        controller.Move(motion * dt);
    }

    void TrackStuck(float dt)
    {
        Vector3 moved = transform.position - lastStuckPosition;
        moved.y = 0f;
        float expected = Mathf.Max(0.0001f, moveSpeed * dt);
        if (moved.magnitude < expected * 0.25f)
        {
            stuckTimer += dt;
            if (stuckTimer >= stuckTimeout)
            {
                previousWaypoint = currentWaypoint;
                currentWaypoint = null;
                waitTimer = 0f;
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
            lastStuckPosition = transform.position;
        }
    }

    void PickNextWaypoint()
    {
        if (waypointGroup == null) return;

        NPCWaypoint next = waypointGroup.GetRandomWaypoint(previousWaypoint);
        if (next == null) return;

        currentWaypoint = next;
        targetPosition = next.transform.position;
        lastStuckPosition = transform.position;
        stuckTimer = 0f;
        waitTimer = 0f;
    }

    void ConfigureCollisionIgnores()
    {
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            Collider playerCollider = player.GetComponent<Collider>();
            if (playerCollider != null) Physics.IgnoreCollision(controller, playerCollider, true);
        }

        GameObject ball = GameObject.Find("Basketball");
        if (ball != null)
        {
            Collider ballCollider = ball.GetComponent<Collider>();
            if (ballCollider != null) Physics.IgnoreCollision(controller, ballCollider, true);
        }
    }
}
