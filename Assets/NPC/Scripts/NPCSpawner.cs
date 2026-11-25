using System.Collections.Generic;
using UnityEngine;

public class NPCSpawner : MonoBehaviour
{
    public GameObject npcPrefab;
    public Transform npcParent;
    public NPCWaypointGroup waypointGroup;
    public List<Transform> spawnPoints = new List<Transform>();
    [Min(0)] public int npcCount = 5;
    public float spawnRadius = 1.25f;
    public Vector2 moveSpeedRange = new Vector2(1.1f, 1.7f);
    public bool ignoreNpcCollisions = true;

    readonly List<GameObject> spawnedNpcs = new List<GameObject>();

    void Start()
    {
        Spawn();
    }

    public void Spawn()
    {
        ClearSpawned();
        EnsureReferences();

        if (npcPrefab == null || spawnPoints.Count == 0 || npcCount <= 0) return;

        Transform parent = npcParent != null ? npcParent : transform;
        for (int i = 0; i < npcCount; i++)
        {
            Transform spawn = spawnPoints[i % spawnPoints.Count];
            if (!TryFindSpawnPosition(spawn.position, out Vector3 position)) continue;

            GameObject npc = Instantiate(npcPrefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent);
            npc.name = npcPrefab.name + "_" + (i + 1).ToString("00");

            NPCWander wander = npc.GetComponent<NPCWander>();
            if (wander != null)
            {
                wander.waypointGroup = waypointGroup;
                wander.moveSpeed = Random.Range(moveSpeedRange.x, moveSpeedRange.y);
            }

            spawnedNpcs.Add(npc);
        }

        if (ignoreNpcCollisions) IgnoreSpawnedCollisions();
    }

    void EnsureReferences()
    {
        if (waypointGroup == null && transform.parent != null)
        {
            Transform waypointRoot = transform.parent.Find("NPCWaypoints");
            if (waypointRoot != null) waypointGroup = waypointRoot.GetComponent<NPCWaypointGroup>();
        }

        if (spawnPoints.Count == 0 && transform.parent != null)
        {
            Transform spawnRoot = transform.parent.Find("SpawnPoints");
            if (spawnRoot != null)
            {
                for (int i = 0; i < spawnRoot.childCount; i++) spawnPoints.Add(spawnRoot.GetChild(i));
            }
        }
    }

    bool TryFindSpawnPosition(Vector3 origin, out Vector3 position)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = origin + new Vector3(offset.x, 0f, offset.y);

            if (!Physics.Raycast(new Vector3(candidate.x, origin.y + 20f, candidate.z), Vector3.down,
                    out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore))
            {
                continue;
            }

            candidate.y = hit.point.y + 0.1f;
            if (IsSpawnClear(candidate))
            {
                position = candidate;
                return true;
            }
        }

        position = origin;
        return false;
    }

    static bool IsSpawnClear(Vector3 position)
    {
        Vector3 bottom = position + Vector3.up * 0.45f;
        Vector3 top = position + Vector3.up * 1.35f;
        return !Physics.CheckCapsule(bottom, top, 0.3f, ~0, QueryTriggerInteraction.Ignore);
    }

    void IgnoreSpawnedCollisions()
    {
        for (int i = 0; i < spawnedNpcs.Count; i++)
        {
            Collider a = spawnedNpcs[i].GetComponent<Collider>();
            if (a == null) continue;

            for (int j = i + 1; j < spawnedNpcs.Count; j++)
            {
                Collider b = spawnedNpcs[j].GetComponent<Collider>();
                if (b != null) Physics.IgnoreCollision(a, b, true);
            }
        }
    }

    void ClearSpawned()
    {
        for (int i = 0; i < spawnedNpcs.Count; i++)
        {
            if (spawnedNpcs[i] != null) Destroy(spawnedNpcs[i]);
        }
        spawnedNpcs.Clear();
    }
}
