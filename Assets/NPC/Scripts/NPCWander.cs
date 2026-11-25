using System.Collections.Generic;
using UnityEngine;

public enum NPCState
{
    Idle,
    Walking,
    Waiting,
    PlayerReaction,
    Paused
}

[RequireComponent(typeof(CharacterController))]
public class NPCWander : MonoBehaviour
{
    static readonly List<NPCWander> ActiveNpcs = new List<NPCWander>();

    public NPCWaypointGroup waypointGroup;
    public float moveSpeed = 1.4f;
    public float rotationSpeed = 180f;
    public float stoppingDistance = 0.4f;
    public float waitTimeAtWaypoint = 1.5f;
    public float idleTimeMin = 0.4f;
    public float idleTimeMax = 1.2f;
    public float minWaypointDistance = 4f;
    public float playerDetectionRadius = 5f;
    public float playerLookSpeed = 300f;
    public float playerReactionDuration = 1.5f;
    public float separationRadius = 1.25f;
    public float separationStrength = 0.8f;
    public float gravity = -18f;
    public float stuckTimeout = 1.8f;

    public NPCState State { get; private set; } = NPCState.Idle;
    public bool IsPaused => State == NPCState.Paused;

    CharacterController controller;
    NPCWaypoint currentWaypoint;
    NPCWaypoint previousWaypoint;
    Transform player;
    Vector3 targetPosition;
    Vector3 lastStuckPosition;
    float verticalVelocity;
    float idleTimer;
    float waitTimer;
    float stuckTimer;
    float reactionTimer;
    NPCState stateBeforePause = NPCState.Idle;
    NPCState stateBeforeReaction = NPCState.Idle;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        ActiveNpcs.Add(this);
        lastStuckPosition = transform.position;
    }

    void OnDestroy()
    {
        ActiveNpcs.Remove(this);
    }

    void Start()
    {
        if (waypointGroup == null) waypointGroup = FindAnyObjectByType<NPCWaypointGroup>();
        player = GameObject.Find("Player")?.transform;
        ConfigureCollisionIgnores();
        idleTimer = Random.Range(idleTimeMin, idleTimeMax);
        State = NPCState.Idle;
        RefreshGameState();
    }

    void Update()
    {
        RefreshGameState();
        if (State == NPCState.Paused) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * dt;

        if (UpdatePlayerReaction(dt)) return;

        switch (State)
        {
            case NPCState.Idle:
                idleTimer -= dt;
                MoveWithGravity(Vector3.zero, dt);
                if (idleTimer <= 0f) PickNextWaypoint();
                break;

            case NPCState.Waiting:
                waitTimer -= dt;
                MoveWithGravity(Vector3.zero, dt);
                if (waitTimer <= 0f) EnterIdle();
                break;

            case NPCState.Walking:
                UpdateWalking(dt);
                break;
        }
    }

    public void SetPaused(bool paused)
    {
        if (paused)
        {
            if (State == NPCState.Paused) return;
            stateBeforePause = State;
            State = NPCState.Paused;
            return;
        }

        if (State != NPCState.Paused) return;
        State = stateBeforePause == NPCState.Paused ? NPCState.Idle : stateBeforePause;
    }

    public void ReactToPlayer(float duration)
    {
        if (State == NPCState.Paused || duration <= 0f) return;
        if (State != NPCState.PlayerReaction)
        {
            stateBeforeReaction = State;
            State = NPCState.PlayerReaction;
        }
        reactionTimer = Mathf.Max(reactionTimer, duration);
    }

    void RefreshGameState()
    {
        SetPaused(!CanMoveForCurrentGameState());
    }

    static bool CanMoveForCurrentGameState()
    {
        GameHUD hud = GameHUD.Instance;
        if (hud == null) return false;
        if (!hud.HasGameStarted || hud.IsCountdownActive || hud.IsTimerFinished) return false;
        return Time.timeScale > 0.0001f;
    }

    bool UpdatePlayerReaction(float dt)
    {
        if (player == null)
        {
            if (State == NPCState.PlayerReaction) FinishPlayerReaction();
            return false;
        }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        if (distance <= playerDetectionRadius)
        {
            if (State != NPCState.PlayerReaction)
            {
                stateBeforeReaction = State;
                State = NPCState.PlayerReaction;
            }

            reactionTimer = Mathf.Max(reactionTimer, playerReactionDuration);
            if (distance > 0.01f)
            {
                Quaternion desired = Quaternion.LookRotation(toPlayer / distance, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, playerLookSpeed * dt);
            }
            MoveWithGravity(Vector3.zero, dt);
            return true;
        }

        if (State == NPCState.PlayerReaction)
        {
            reactionTimer -= dt;
            MoveWithGravity(Vector3.zero, dt);
            if (reactionTimer <= 0f) FinishPlayerReaction();
            return true;
        }

        return false;
    }

    void FinishPlayerReaction()
    {
        State = stateBeforeReaction == NPCState.PlayerReaction ? NPCState.Idle : stateBeforeReaction;
        if (State == NPCState.Paused) State = NPCState.Idle;
        reactionTimer = 0f;
    }

    void UpdateWalking(float dt)
    {
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;

        if (distance <= stoppingDistance)
        {
            previousWaypoint = currentWaypoint;
            currentWaypoint = null;
            State = NPCState.Waiting;
            waitTimer = waitTimeAtWaypoint;
            stuckTimer = 0f;
            MoveWithGravity(Vector3.zero, dt);
            return;
        }

        Vector3 direction = toTarget / distance;
        direction = ApplySeparation(direction);

        Quaternion desiredRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, rotationSpeed * dt);

        MoveWithGravity(direction * moveSpeed, dt);
        TrackStuck(dt);
    }

    Vector3 ApplySeparation(Vector3 direction)
    {
        Vector3 separation = Vector3.zero;
        float radiusSqr = separationRadius * separationRadius;

        for (int i = 0; i < ActiveNpcs.Count; i++)
        {
            NPCWander other = ActiveNpcs[i];
            if (other == null || other == this) continue;

            Vector3 offset = transform.position - other.transform.position;
            offset.y = 0f;
            float sqr = offset.sqrMagnitude;
            if (sqr <= 0.0001f || sqr >= radiusSqr) continue;

            float distance = Mathf.Sqrt(sqr);
            separation += offset / distance * (1f - distance / separationRadius);
        }

        if (separation.sqrMagnitude > 0.0001f)
        {
            direction = (direction + separation * separationStrength).normalized;
        }
        return direction;
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
                stuckTimer = 0f;
                EnterIdle();
            }
        }
        else
        {
            stuckTimer = 0f;
            lastStuckPosition = transform.position;
        }
    }

    void EnterIdle()
    {
        State = NPCState.Idle;
        idleTimer = Random.Range(idleTimeMin, idleTimeMax);
    }

    void PickNextWaypoint()
    {
        if (waypointGroup == null)
        {
            EnterIdle();
            return;
        }

        NPCWaypoint next = waypointGroup.GetRandomWaypoint(previousWaypoint, transform.position, minWaypointDistance);
        if (next == null)
        {
            EnterIdle();
            return;
        }

        currentWaypoint = next;
        targetPosition = next.transform.position;
        lastStuckPosition = transform.position;
        stuckTimer = 0f;
        waitTimer = 0f;
        State = NPCState.Walking;
    }

    void ConfigureCollisionIgnores()
    {
        GameObject playerObject = GameObject.Find("Player");
        if (playerObject != null)
        {
            Collider playerCollider = playerObject.GetComponent<Collider>();
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
