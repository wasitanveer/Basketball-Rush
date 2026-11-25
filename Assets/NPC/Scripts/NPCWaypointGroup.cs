using System.Collections.Generic;
using UnityEngine;

public class NPCWaypointGroup : MonoBehaviour
{
    public List<NPCWaypoint> waypoints = new List<NPCWaypoint>();

    void Awake()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (waypoints == null) waypoints = new List<NPCWaypoint>();
        if (waypoints.Count > 0) return;

        NPCWaypoint[] children = GetComponentsInChildren<NPCWaypoint>(true);
        for (int i = 0; i < children.Length; i++)
        {
            waypoints.Add(children[i]);
        }
    }

    public NPCWaypoint GetRandomWaypoint(NPCWaypoint previous)
    {
        Refresh();
        int count = waypoints.Count;
        if (count == 0) return null;
        if (count == 1) return waypoints[0];

        for (int attempt = 0; attempt < 12; attempt++)
        {
            NPCWaypoint candidate = waypoints[Random.Range(0, count)];
            if (candidate != null && candidate != previous) return candidate;
        }

        for (int i = 0; i < count; i++)
        {
            NPCWaypoint candidate = waypoints[i];
            if (candidate != null && candidate != previous) return candidate;
        }

        return waypoints[0];
    }
}
