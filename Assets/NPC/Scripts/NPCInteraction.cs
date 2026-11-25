using System.Collections.Generic;
using UnityEngine;

public class NPCInteraction : MonoBehaviour
{
    public static readonly List<NPCInteraction> Active = new List<NPCInteraction>();

    public string[] lines = { "Hey!", "Nice shot!", "What's up?", "Good game!" };

    NPCWander wander;

    void Awake()
    {
        Active.Add(this);
        wander = GetComponent<NPCWander>();
    }

    void OnDestroy()
    {
        Active.Remove(this);
    }

    public string Interact()
    {
        if (wander == null) wander = GetComponent<NPCWander>();
        if (wander != null) wander.ReactToPlayer(2.5f);
        if (lines == null || lines.Length == 0) return "Hello!";
        return lines[Random.Range(0, lines.Length)];
    }
}
