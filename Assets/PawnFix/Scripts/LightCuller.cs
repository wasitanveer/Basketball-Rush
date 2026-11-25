using System.Collections.Generic;
using UnityEngine;

namespace PawnFix
{
    /// Mobile light budget: keeps only the street lights nearest the camera switched on.
    /// The glowing bulb meshes stay visible, so far lamps still read as lit.
    public class LightCuller : MonoBehaviour
    {
        public Transform viewer;
        public int maxLights = 12;          // point/spot lights live at once
        public float maxDistance = 40f;     // beyond this a lamp's pool of light is not worth its cost
        public int maxShadowed = 1;         // floodlights that may cast shadows at once
        public float interval = 0.25f;

        Light[] lights;
        LightShadows[] shadowMode;
        readonly List<int> order = new List<int>();
        float[] dist;
        float next;

        void Awake()
        {
            lights = GetComponentsInChildren<Light>(true);
            shadowMode = new LightShadows[lights.Length];
            for (int i = 0; i < lights.Length; i++) shadowMode[i] = lights[i].shadows;
            dist = new float[lights.Length];
            if (!viewer) viewer = Camera.main ? Camera.main.transform : null;
        }

        void Update()
        {
            if (!viewer || Time.unscaledTime < next) return;
            next = Time.unscaledTime + interval;
            Vector3 p = viewer.position, f = viewer.forward;
            order.Clear();
            for (int i = 0; i < lights.Length; i++)
            {
                var d = lights[i].transform.position - p;
                // lights behind the viewer count as further away, so the ones in view win the budget
                float w = d.magnitude - lights[i].range * 0.5f;
                if (Vector3.Dot(d, f) < 0) w += 12f;
                dist[i] = w;
                order.Add(i);
            }
            order.Sort((a, b) => dist[a].CompareTo(dist[b]));
            int shadows = 0;
            for (int k = 0; k < order.Count; k++)
            {
                int i = order[k];
                bool on = k < maxLights && dist[i] < maxDistance;
                if (lights[i].enabled != on) lights[i].enabled = on;
                if (on && shadowMode[i] != LightShadows.None)
                    lights[i].shadows = shadows++ < maxShadowed ? shadowMode[i] : LightShadows.None;
            }
        }
    }
}
