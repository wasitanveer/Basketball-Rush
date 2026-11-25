using UnityEngine;

public class NPCWaypoint : MonoBehaviour
{
    public float weight = 1f;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 0.7f);
    }
}
