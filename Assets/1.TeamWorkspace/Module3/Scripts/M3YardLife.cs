using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The moving parts of the village dressing: chickens scratching about the yards and the lane, and
/// washing swinging on its lines. Built by M3VillageDressing, which hands it every hen and every
/// laundry line; this is the one Update that moves them.
///
/// Decoration only, like M3OutdoorLife: nothing here has a collider or is in the simulation. A hen
/// keeps to a small radius round its home, pecks between strolls, and scatters when the Pilot walks
/// up. Past <see cref="HenRadius"/> from the camera a hen is switched off; the washing is a few
/// transforms turned by a sine and costs nothing worth measuring.
/// </summary>
public class M3YardLife : MonoBehaviour
{
    /// <summary>Beyond this many metres from the camera a hen is switched off (and a little nearer, on again).</summary>
    private const float HenRadius = 32f, Hysteresis = 4f;
    /// <summary>A hen this close to the Pilot's head runs off.</summary>
    private const float Startle = 1.4f;

    private struct Hen
    {
        public Transform body, head;
        public Vector3 home, target;
        public float radius, speed, phase;
        public float nextPeck, peckStart, nextStroll;
        public bool walking, fleeing, active;
    }

    private struct Line
    {
        public Transform pivot;
        public float phase, swing;
    }

    private struct Sleeper
    {
        public Transform t;
        public Vector3 scale;
        public float phase;
    }

    private readonly List<Hen> hens = new List<Hen>();
    private readonly List<Line> lines = new List<Line>();
    private readonly List<Sleeper> sleepers = new List<Sleeper>();
    private System.Random rng;
    private Camera eye;

    public int HenCount => hens.Count;
    public int LineCount => lines.Count;

    public void Init(int seed) => rng = new System.Random(seed * 131 + 7);

    /// <summary>A hen whose body and head <see cref="M3VillageDressing"/> has already built, standing at <paramref name="home"/>.</summary>
    public void AddHen(Transform body, Transform head, Vector3 home, float radius)
    {
        if (rng == null) Init(0);
        float now = Time.time;
        hens.Add(new Hen
        {
            body = body, head = head, home = home, target = home, radius = radius,
            speed = 0.35f, phase = (float)rng.NextDouble() * 10f,
            nextPeck = now + (float)rng.NextDouble() * 2f, peckStart = -1f,
            nextStroll = now + 1f + (float)rng.NextDouble() * 6f, active = true,
        });
    }

    /// <summary>A washing line's pivot: its local x runs along the line, the cloth hangs below.</summary>
    public void AddLine(Transform pivot)
    {
        if (rng == null) Init(0);
        lines.Add(new Line { pivot = pivot, phase = (float)rng.NextDouble() * 10f, swing = 2.5f + (float)rng.NextDouble() * 2.5f });
    }

    /// <summary>A sleeping animal: it breathes, nothing more.</summary>
    public void AddSleeper(Transform t)
    {
        if (rng == null) Init(0);
        sleepers.Add(new Sleeper { t = t, scale = t.localScale, phase = (float)rng.NextDouble() * 10f });
    }

    private void Update()
    {
        float now = Time.time, dt = Time.deltaTime;

        for (int i = 0; i < sleepers.Count; i++)
        {
            var s = sleepers[i];
            if (!s.t.gameObject.activeInHierarchy) continue;
            float breath = 1f + 0.03f * Mathf.Sin(now * 2.1f + s.phase);
            s.t.localScale = new Vector3(s.scale.x, s.scale.y * breath, s.scale.z);
        }
        if (eye == null || (Time.frameCount & 31) == 0) eye = Camera.main;
        bool haveEye = eye != null;
        var eyeAt = haveEye ? eye.transform.position : Vector3.zero;

        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            // A gust every so often on top of the steady swing: two sines that drift in and out of step.
            float a = Mathf.Sin(now * 1.1f + l.phase) * l.swing + Mathf.Sin(now * 2.7f + l.phase * 1.7f) * 1.2f;
            l.pivot.localRotation = Quaternion.AngleAxis(a, Vector3.right);
        }

        for (int i = 0; i < hens.Count; i++)
        {
            var h = hens[i];
            var at = h.body.position;
            if (!Reach(ref h.active, at, eyeAt, haveEye, h.body.gameObject)) { hens[i] = h; continue; }

            if (haveEye)
            {
                float dx = at.x - eyeAt.x, dz = at.z - eyeAt.z;
                if (!h.fleeing && dx * dx + dz * dz < Startle * Startle)
                {
                    // Off, away from the Pilot, to the far side of the home patch.
                    var away = new Vector3(dx, 0f, dz);
                    if (away.sqrMagnitude < 1e-4f) away = h.body.forward;
                    h.target = Clamp(h.home, at + away.normalized * (h.radius + 0.8f), h.radius);
                    h.fleeing = true;
                    h.walking = true;
                    h.speed = 1.1f;
                    h.peckStart = -1f;
                    h.head.localRotation = Quaternion.identity;
                }
            }

            if (h.walking)
            {
                var to = h.target - at;
                to.y = 0f;
                if (to.sqrMagnitude < 0.01f)
                {
                    h.walking = false;
                    h.fleeing = false;
                    h.speed = 0.35f;
                    h.body.position = new Vector3(at.x, h.home.y, at.z);
                    h.nextStroll = now + 2f + (float)rng.NextDouble() * 7f;
                    h.nextPeck = now + 0.4f;
                }
                else
                {
                    h.body.rotation = Quaternion.RotateTowards(h.body.rotation, Quaternion.LookRotation(to, Vector3.up), 360f * dt);
                    var next = Vector3.MoveTowards(at, h.target, h.speed * dt);
                    // A hen bobs as it walks; the head pumps with the step.
                    float step = now * (h.fleeing ? 16f : 9f) + h.phase;
                    next.y = h.home.y + Mathf.Abs(Mathf.Sin(step)) * 0.012f;
                    h.body.position = next;
                    h.head.localRotation = Quaternion.AngleAxis(10f + 10f * Mathf.Sin(step), Vector3.right);
                }
            }
            else
            {
                if (now >= h.nextStroll)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float r = 0.4f + (float)rng.NextDouble() * h.radius;
                    h.target = Clamp(h.home, h.home + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), h.radius);
                    h.walking = true;
                    h.peckStart = -1f;
                }
                else if (h.peckStart >= 0f)
                {
                    float p = (now - h.peckStart) / 0.32f;
                    if (p >= 1f)
                    {
                        h.peckStart = -1f;
                        h.nextPeck = now + 0.4f + (float)rng.NextDouble() * 1.4f;
                        h.head.localRotation = Quaternion.identity;
                    }
                    else
                    {
                        // Down fast, up a little slower: a dip to the ground and back.
                        float dip = p < 0.4f ? p / 0.4f : 1f - (p - 0.4f) / 0.6f;
                        h.head.localRotation = Quaternion.AngleAxis(62f * dip, Vector3.right);
                    }
                }
                else if (now >= h.nextPeck)
                {
                    h.peckStart = now;
                    // Turn a little between pecks, as a scratching hen does.
                    h.body.rotation *= Quaternion.AngleAxis(((float)rng.NextDouble() - 0.5f) * 40f, Vector3.up);
                }
            }
            hens[i] = h;
        }
    }

    private static Vector3 Clamp(Vector3 home, Vector3 p, float radius)
    {
        var d = p - home;
        d.y = 0f;
        if (d.magnitude > radius) d = d.normalized * radius;
        return new Vector3(home.x + d.x, home.y, home.z + d.z);
    }

    private static bool Reach(ref bool active, Vector3 at, Vector3 eyeAt, bool haveEye, GameObject go)
    {
        if (!haveEye) return active;
        float dx = at.x - eyeAt.x, dz = at.z - eyeAt.z;
        float d2 = dx * dx + dz * dz;
        float limit = active ? HenRadius + Hysteresis : HenRadius;
        bool near = d2 < limit * limit;
        if (near != active)
        {
            active = near;
            go.SetActive(near);
        }
        return active;
    }
}
