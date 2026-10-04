using System.Collections.Generic;

using UnityEngine;
using UnityEngine.XR;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
public class PlayerMain : MonoBehaviour
{
    public static PlayerMain instance;
    public UnityEngine.XR.InputDevice _rightController;
    public UnityEngine.XR.InputDevice _leftController;
    public UnityEngine.XR.InputDevice _HMD;
    public Slider BloodBar,NectarBar;

    public Rigidbody rb;

    public ParticleSystem LayEggparti, MateParti, DrinkBloodParti, DrinknectarParti;

    public GameObject mainCamera, CamRot;
    public GameObject termalcam,ui;

    public float Speed, FlyUpSpeed,CamRotSpeed;

    [Tooltip("How much looking up or down steers flight. 1 = fly exactly where you look, 0 = always level.")]
    [Range(0f, 1f)] public float PitchInfluence = 1f;
    [Tooltip("Degrees of head pitch ignored around the horizon, so glancing slightly off level still flies level.")]
    public float PitchDeadZone = 6f;

    [Header("Collision")]
    [Tooltip("Gap kept between the mosquito and any surface it flies into.")]
    public float SkinWidth = 0.01f;
    [Tooltip("Drop below this height and the mosquito is put back where it was last safely above ground.")]
    public float FallThroughY = -3f;

    [Header("Interaction reach")]
    [Tooltip("How far from the head the mosquito can reach to drink, mate or lay eggs. The proboscis sticks out about 0.2.")]
    public float InteractionRange = 0.5f;
    [Tooltip("Inside this distance a target counts whichever way you happen to be looking.")]
    public float TouchRange = 0.2f;
    [Tooltip("How far off your look direction a target may sit and still count, in degrees.")]
    [Range(10f, 180f)] public float InteractionAngle = 80f;
    [Tooltip("Extra reach kept while feeding, so drifting a little does not break a drink.")]
    public float FeedingReachBonus = 0.3f;
    [Tooltip("A press of A still fires if a target comes into reach this soon afterwards, so you can press just before you arrive.")]
    public float PrimaryBufferTime = 0.3f;

    public float Current_Blood, Max_Blood;
    public float Current_Nec, Max_Nec;

    public bool R_primaryValue,L_primaryValue, R_secondary, L_secondary, R_gripValue,L_gripValue, R_triggerValue, L_triggerValue,IsMoveL,IsMoveR;
    public bool termalmode,canmove;
    public bool isMate,Death,RestartAble;
    public bool ishungry,testClick;

    public int EggLayed;

    public string DeathMessage;

    public Vector2 L_moveInput, R_moveInput;
    public SendReceiveMessageExample sr;

    public List<WaterContainer> WC ;

    /// <summary>What the player is currently in reach of.</summary>
    public enum Interaction { None, Flower, Human, Mate, FemaleMosquito, Container }
    public Interaction CurrentInteraction { get; private set; }
    public WaterContainer CurrentContainer { get; private set; }
    /// <summary>The flower or person in reach, to ride along with while feeding on it.</summary>
    public Transform CurrentHost { get; private set; }
    /// <summary>A/X went down this frame (either hand).</summary>
    public bool PrimaryDown { get; private set; }
    /// <summary>A/X held right now (either hand).</summary>
    public bool PrimaryHeld { get; private set; }

    /// <summary>
    /// Blood one egg costs. A single full blood meal is one clutch of
    /// <see cref="QuestSystem.EggsToLay"/> eggs, so laying spends a quarter of the bar at a time
    /// instead of emptying it - the player can lay the whole clutch off one feed.
    /// </summary>
    public float BloodPerEgg => QuestSystem.EggsToLay > 0 ? Max_Blood / QuestSystem.EggsToLay : Max_Blood;
    /// <summary>Enough blood left for one more egg. Small tolerance so 4 eggs really fit in one meal.</summary>
    public bool HasBloodForEgg => Current_Blood >= BloodPerEgg - 0.01f;

    /// <summary>Alive, with time left and not on a death / time-out screen.</summary>
    public bool Playing => !Death && !RestartAble && GameManager.instance != null && GameManager.instance.time > 0;

    private bool primaryHeldLastFrame;
    /// <summary>When A was last pressed, so a press can be honoured slightly before arrival.</summary>
    private float primaryPressedAt = float.NegativeInfinity;

    /// <summary>One thing the player can act on, with its collider and outline resolved once.</summary>
    private struct Target
    {
        public Interaction kind;
        public Transform transform;
        public Collider collider;
        public WaterContainer container;
        public global::Outline outline;
    }

    private readonly List<Target> targets = new List<Target>();
    private float targetsRefreshAt;
    /// <summary>Nothing in the village appears or disappears quickly, so a rescan every two seconds is plenty.</summary>
    private const float TargetRefreshInterval = 2f;
    private global::Outline highlighted;
    private global::Outline currentOutline;
    private bool feedingLastFrame;

    /// <summary>How fast a surface may push the mosquito back out if it has ended up inside one.</summary>
    private const float MaxSeparationSpeed = 2f;
    /// <summary>Corners need more than one pass to resolve; three is plenty for village geometry.</summary>
    private const int SlideIterations = 3;

    private CapsuleCollider bodyCollider;
    private Collider[] ownColliders;
    private readonly Collider[] overlaps = new Collider[16];
    private readonly RaycastHit[] sweepHits = new RaycastHit[16];
    private Vector3 desiredVelocity;
    private Vector3 lastGroundedPosition;

    private void Awake()
    {
        canmove = true;
        instance = this;
        rb = GetComponent<Rigidbody>();
        // The headset camera rides this body. Without interpolation it is only redrawn on the
        // 50 Hz physics clock while the headset renders at 72-90 Hz, which reads as judder.
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        // Backstop for the sweep below: discrete detection can step straight over thin geometry.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        bodyCollider = GetComponent<CapsuleCollider>();
        ownColliders = GetComponentsInChildren<Collider>(true);
        lastGroundedPosition = transform.position;
        BloodBar.maxValue = Max_Blood;
        BloodBar.value = Current_Blood;
        NectarBar.maxValue = Max_Nec;
        Current_Nec = Max_Nec/2;
        NectarBar.value = Current_Nec;
        StripDeviceSimulatorFromBuild();
    }
    private void Start()
    {
        foreach (WaterContainer w in GameManager.instance.waterContainers)
        {
            if (w.isFill)
            {
                WC.Add(w);
            }
        }
        Invoke("BornFromWater",0.25f);
    }

    /// <summary>
    /// The XR Device Simulator registers an <c>XRSimulatedHMD</c>, which is an <c>XRHMD</c> just
    /// like the real headset. Left active in a build, the camera's TrackedPoseDriver can bind to
    /// that fake HMD instead of the Quest: the view then freezes perfectly level and the world
    /// appears glued to the player's head. It is an editor-only testing aid, so make sure a stray
    /// enabled checkbox can never ship it to the device.
    /// </summary>
    private void StripDeviceSimulatorFromBuild()
    {
        if (Application.isEditor) return;
        var sim = FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulator>();
        if (sim == null) return;
        sim.gameObject.SetActive(false);
        Debug.LogWarning("[PlayerMain] XR Device Simulator was active in a build - disabled it so head tracking works.");
    }

    void Update()
    {
        if(Camera.main.transform.localPosition!= Vector3.zero)
        {
            Camera.main.transform.localPosition = Vector3.zero;
        }
        checkinput();
        if (R_primaryValue && L_primaryValue && R_triggerValue && L_triggerValue)
        {
            BornFromWater();
        }
        if (Playing)
        {
            termalcam.SetActive(R_triggerValue);
            AcquireTarget();
            UpdateFeeding();   // sets canmove, so it has to come before Move
            TryPrimaryAction();
            Move(L_moveInput);

            if (L_gripValue )
            {

                GameManager.instance.questUI.SetActive(L_triggerValue);
            }
            NectarUPdate();
        }
        else
        {
            desiredVelocity = Vector3.zero;
            // Do not ride a walking person around on the death / time-out screen.
            canmove = true;
            Detach();
            ReportInteraction(Interaction.None);
        }
        UpdateHighlight();
        BloodBar.value = Current_Blood;
        NectarBar.value = Current_Nec;
    }

    /// <summary>
    /// The rigidbody is driven, not accumulated, so it has to be written on the physics clock.
    /// Writing it from <see cref="Update"/> re-forced the velocity between every physics step and
    /// so wiped out the contact resolution that pushes the mosquito back out of a surface it had
    /// begun to sink into - a few steps of that and it was through the floor for good.
    /// </summary>
    private void FixedUpdate()
    {
        if (onclick(R_primaryValue))
        {

        }
        if (!rb.isKinematic)
            rb.linearVelocity = bodyCollider == null
                ? desiredVelocity
                : SlideAlongSurfaces(desiredVelocity) + SeparationVelocity();
        TrackFallThrough();
    }
    public void NectarUPdate()
    {
        if (Current_Nec > 0)
        {
            Current_Nec -= Time.deltaTime / Max_Nec;
        }
        else
        {
            GameManager.instance.GameOver(DeathMessage);
        }

        if (Current_Nec < Max_Nec / 2)
        {
            ishungry = true;
        }
    }

    /// <summary>
    /// Rides along with what the mosquito is feeding on. The body goes kinematic for the duration:
    /// a dynamic rigidbody parented to a moving transform is teleported every frame, which jitters
    /// and pushes straight through anything in the way.
    /// </summary>
    public void AttachTo(Transform host)
    {
        if (transform.parent == host) return;
        desiredVelocity = Vector3.zero;
        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = true;
        transform.parent = host;
    }

    /// <summary>Releases the mosquito from <see cref="AttachTo"/> and hands it back to physics.</summary>
    public void Detach()
    {
        if (transform.parent == null && !rb.isKinematic) return;
        transform.parent = null;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
    }

    /// <summary>
    /// Takes the part of <paramref name="velocity"/> that would cross a surface this physics step
    /// and turns it into motion along that surface. The velocity here is driven rather than
    /// accumulated, so anything the solver does to resolve a contact is overwritten on the next
    /// step anyway. The upshot for the player is sliding along a fence or a wall instead of
    /// buzzing against it.
    /// </summary>
    private Vector3 SlideAlongSurfaces(Vector3 velocity)
    {
        float dt = Time.fixedDeltaTime;
        for (int i = 0; i < SlideIterations; i++)
        {
            float speed = velocity.magnitude;
            if (speed < 0.0001f) return Vector3.zero;

            Vector3 dir = velocity / speed;
            float step = speed * dt;
            if (!SweepAhead(dir, step + SkinWidth, out float hitDistance, out Vector3 normal))
                break;

            // Travel as far as the surface allows, then spend what is left of the step sliding
            // along it, so contact costs you the approach but not the rest of your speed.
            // The probe is SkinWidth thinner than the body, so one SkinWidth here just undoes
            // that; the second is the clearance the body actually comes to rest with.
            float allowed = Mathf.Clamp(hitDistance - 2f * SkinWidth, 0f, step);
            Vector3 next = (dir * allowed + Vector3.ProjectOnPlane(dir * (step - allowed), normal)) / dt;
            if ((next - velocity).sqrMagnitude < 0.00000001f) break;
            velocity = next;
        }
        return velocity;
    }

    /// <summary>
    /// Nearest surface the body would reach travelling <paramref name="dir"/>, ignoring its own
    /// colliders and triggers. The probe is a hair thinner than the body so a surface already
    /// being rested against reports a usable normal instead of a zero-distance hit.
    /// </summary>
    private bool SweepAhead(Vector3 dir, float distance, out float hitDistance, out Vector3 normal)
    {
        hitDistance = 0f;
        normal = Vector3.zero;
        BodyCapsule(out Vector3 p0, out Vector3 p1, out float radius);
        int n = Physics.CapsuleCastNonAlloc(p0, p1, Mathf.Max(0.001f, radius - SkinWidth), dir,
                                            sweepHits, distance, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            // Zero distance means the probe began inside that collider, so its normal means
            // nothing; SeparationVelocity is what deals with that case.
            if (sweepHits[i].distance <= 0f || sweepHits[i].distance >= best) continue;
            if (IsOwn(sweepHits[i].collider)) continue;
            best = sweepHits[i].distance;
            normal = sweepHits[i].normal;
        }
        hitDistance = best;
        return best < float.MaxValue;
    }

    /// <summary>
    /// Lifts the body out of anything it has ended up inside. PhysX does this for a body it owns,
    /// but writing the velocity every physics step wipes out the separating velocity it applied.
    /// </summary>
    private Vector3 SeparationVelocity()
    {
        BodyCapsule(out Vector3 p0, out Vector3 p1, out float radius);
        int n = Physics.OverlapCapsuleNonAlloc(p0, p1, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
        Vector3 push = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            if (IsOwn(overlaps[i])) continue;
            if (Physics.ComputePenetration(
                    bodyCollider, bodyCollider.transform.position, bodyCollider.transform.rotation,
                    overlaps[i], overlaps[i].transform.position, overlaps[i].transform.rotation,
                    out Vector3 dir, out float depth)
                && depth > 0.0001f)
                push += dir * depth;
        }
        return Vector3.ClampMagnitude(push / Time.fixedDeltaTime, MaxSeparationSpeed);
    }

    private bool IsOwn(Collider other)
    {
        for (int i = 0; i < ownColliders.Length; i++)
            if (ownColliders[i] == other) return true;
        return false;
    }

    /// <summary>The body capsule in world space: its two sphere centres and its radius.</summary>
    private void BodyCapsule(out Vector3 p0, out Vector3 p1, out float radius)
    {
        Transform t = bodyCollider.transform;
        Vector3 scale = t.lossyScale;
        radius = bodyCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        // m_Direction is Y on this collider. Height below twice the radius is just a sphere,
        // which is what the rig actually ships with, and then both centres coincide.
        float half = Mathf.Max(0f, bodyCollider.height * 0.5f * Mathf.Abs(scale.y) - radius);
        Vector3 center = t.TransformPoint(bodyCollider.center);
        p0 = center - t.up * half;
        p1 = center + t.up * half;
    }

    /// <summary>
    /// Remembers the last position with ground under it, and returns there if the mosquito ends up
    /// below the world anyway - a scripted teleport, or a spawn point left under the ground.
    /// </summary>
    private void TrackFallThrough()
    {
        if (transform.position.y < FallThroughY)
        {
            Detach();
            canmove = true;
            Teleport(lastGroundedPosition);
            return;
        }
        if (bodyCollider == null) return;
        // A ray starting inside a collider does not hit it, so this cannot catch the mosquito.
        Vector3 center = bodyCollider.transform.TransformPoint(bodyCollider.center);
        if (Physics.Raycast(center, Vector3.down, out _, 3f, ~0, QueryTriggerInteraction.Ignore))
            lastGroundedPosition = transform.position;
    }

    /// <summary>Moves the body without interpolation smearing the jump across the next frame.</summary>
    private void Teleport(Vector3 position)
    {
        rb.interpolation = RigidbodyInterpolation.None;
        rb.position = position;
        transform.position = position;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        desiredVelocity = Vector3.zero;
    }

    public void ReportInteraction(Interaction kind, WaterContainer container = null, Transform host = null, global::Outline outline = null)
    {
        CurrentInteraction = kind;
        CurrentContainer = container;
        CurrentHost = host;
        currentOutline = outline;
    }

    /// <summary>
    /// Picks what the player is reaching for: whatever is within <see cref="InteractionRange"/> of
    /// the head and roughly in front of it.
    ///
    /// This replaces the proboscis trigger volume everything used to run on, which is far too
    /// tight for a fast-moving flyer: the snout is a capsule about 0.2 long and a few millimetres
    /// thick, so drinking meant spearing a flower almost exactly through the middle, and laying or
    /// mating needed the snout inside the target's own collider.
    /// </summary>
    private void AcquireTarget()
    {
        if (Time.time >= targetsRefreshAt) RefreshTargets();

        Vector3 origin = mainCamera != null ? mainCamera.transform.position : transform.position;
        Vector3 look = mainCamera != null ? mainCamera.transform.forward : transform.forward;
        float range = InteractionRange + (transform.parent != null ? FeedingReachBonus : 0f);

        int best = -1;
        float bestScore = float.MaxValue;

        for (int i = 0; i < targets.Count; i++)
        {
            Target t = targets[i];
            if (t.transform == null || !t.transform.gameObject.activeInHierarchy) continue;

            // Bounds, not the exact shape: ClosestPoint throws on a concave mesh collider, and the
            // bounding box is the forgiving reading, which is the point here. Inside the box this
            // returns the origin itself, so flying into a jar always counts.
            Vector3 point = t.collider != null ? t.collider.ClosestPointOnBounds(origin) : t.transform.position;
            Vector3 to = point - origin;
            float distance = to.magnitude;
            if (distance > range) continue;

            float angle = distance > 0.001f ? Vector3.Angle(look, to) : 0f;
            if (angle > InteractionAngle && distance > TouchRange) continue;

            // Nearest wins, except that something usable right now beats something that is not,
            // and facing it breaks ties.
            float score = distance * (1f + angle / 180f) * (IsUseful(t) ? 1f : 2.5f);
            // Stay locked on to whatever we are already feeding on, so a second flower right next
            // to the first one cannot steal the drink mid-sip.
            if (transform.parent == t.transform) score *= 0.5f;
            if (score >= bestScore) continue;

            bestScore = score;
            best = i;
        }

        if (best < 0) ReportInteraction(Interaction.None);
        else ReportInteraction(targets[best].kind, targets[best].container, targets[best].transform, targets[best].outline);
    }

    /// <summary>Whether acting on this target would actually do anything right now.</summary>
    private bool IsUseful(Target t)
    {
        switch (t.kind)
        {
            case Interaction.Flower: return Current_Nec < Max_Nec;
            case Interaction.Human: return Current_Blood < Max_Blood;
            case Interaction.Mate: return !isMate;
            case Interaction.Container:
                return t.container != null && t.container.isFill && isMate && HasBloodForEgg;
            default: return false;
        }
    }

    /// <summary>Rebuilds the list of things in the level worth reaching for, resolving each one's collider once.</summary>
    private void RefreshTargets()
    {
        targetsRefreshAt = Time.time + TargetRefreshInterval;
        targets.Clear();

        foreach (var flower in GameObject.FindGameObjectsWithTag("Flower"))
            AddTarget(Interaction.Flower, flower.transform, null);
        foreach (var human in FindObjectsByType<Human>(FindObjectsSortMode.None))
            AddTarget(Interaction.Human, human.transform, null);
        foreach (var wild in FindObjectsByType<Wild_Mosquitos>(FindObjectsSortMode.None))
            AddTarget(wild.Gender == Wild_Mosquitos.genderlist.male ? Interaction.Mate : Interaction.FemaleMosquito,
                      wild.transform, null);
        foreach (var container in FindObjectsByType<WaterContainer>(FindObjectsSortMode.None))
            AddTarget(Interaction.Container, container.transform, container);
    }

    private void AddTarget(Interaction kind, Transform t, WaterContainer container)
    {
        var col = t.GetComponent<Collider>();
        if (col == null) col = t.GetComponentInChildren<Collider>();
        var outline = t.GetComponent<global::Outline>();
        if (outline == null) outline = t.GetComponentInParent<global::Outline>();
        if (outline == null) outline = t.GetComponentInChildren<global::Outline>(true);
        targets.Add(new Target { kind = kind, transform = t, collider = col, container = container, outline = outline });
    }

    /// <summary>
    /// Outlines whatever is in reach, so the player can see what A will act on. This used to be
    /// switched by the proboscis trigger touching the object (InteractableObject), which no longer
    /// matches what A acts on now that reach is measured from the head.
    /// </summary>
    private void UpdateHighlight()
    {
        if (highlighted == currentOutline) return;
        if (highlighted != null) highlighted.enabled = false;
        highlighted = currentOutline;
        if (highlighted != null) highlighted.enabled = true;
    }

    /// <summary>
    /// Hold A on a person or a flower to feed, riding along with them while you do. This used to
    /// live in <see cref="Drink"/> on the proboscis trigger; it runs off the reach test now.
    /// </summary>
    private void UpdateFeeding()
    {
        bool feeding = false;
        if (PrimaryHeld && CurrentHost != null)
        {
            if (CurrentInteraction == Interaction.Human && Current_Blood < Max_Blood)
            {
                Drink();
                feeding = true;
            }
            else if (CurrentInteraction == Interaction.Flower && Current_Nec < Max_Nec)
            {
                if (!feedingLastFrame) DrinknectarParti.Play();
                DrinkNectar();
                feeding = true;
            }
        }
        feedingLastFrame = feeding;

        if (feeding)
        {
            canmove = false;
            AttachTo(CurrentHost);
            return;
        }

        // Not feeding: stay put only while A is still held on the same host - the bar may just have
        // filled up. Otherwise let go and fly again.
        if (!PrimaryHeld || CurrentHost == null || transform.parent != CurrentHost)
        {
            canmove = true;
            Detach();
        }
    }

    /// <summary>
    /// One press of A does one thing to whatever is in reach: mate with a male, or lay an egg in
    /// a container. The press is buffered for <see cref="PrimaryBufferTime"/> and consumed on
    /// success, so pressing just before you arrive works and one press lays one egg. Feeding is not
    /// here: that is a hold, and <see cref="UpdateFeeding"/> handles it.
    /// </summary>
    private void TryPrimaryAction()
    {
        if (Time.time - primaryPressedAt > PrimaryBufferTime) return;

        switch (CurrentInteraction)
        {
            case Interaction.Mate:
                if (isMate) return;
                isMate = true;
                MateParti.Play();
                break;
            case Interaction.Container:
                if (!TryLayEgg(CurrentContainer)) return;
                break;
            default:
                return;
        }
        primaryPressedAt = float.NegativeInfinity;
    }

    /// <summary>
    /// Lays a single egg, if the player has mated and has blood left for it. Eggs need standing
    /// water, so containers only count once the rain has filled them.
    /// </summary>
    private bool TryLayEgg(WaterContainer container)
    {
        if (container == null || !container.isFill) return false;
        if (!isMate || !HasBloodForEgg) return false;

        Current_Blood = Mathf.Max(0f, Current_Blood - BloodPerEgg);
        EggLayed++;
        LayEggparti.Play();
        GameManager.instance.LayEggScore += container.Score;
        GameManager.instance.setscore(container.Score);
        return true;
    }

    public void checkinput()
    {
        if (!_rightController.isValid || !_leftController.isValid || !_HMD.isValid)
            InitializeInputDevices();
        if (_rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out R_triggerValue) && R_triggerValue)
        {

        }
        if (_leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out L_triggerValue) && L_triggerValue)
        {

        }
        if (_rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out R_gripValue) && R_gripValue)
        {

        }
        if (_leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out L_gripValue) && L_gripValue)
        {

        }
        if (_rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out R_secondary) && R_secondary)
        {

        }
        if (_leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out L_secondary) && L_secondary)
        {

        }
        if (_rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out R_primaryValue) && R_primaryValue)
        {

        }
        if (_leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out L_primaryValue) && L_primaryValue)
        {

        }
        if (_leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out L_moveInput) && IsMoveL)
        {

        }
        if (_rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out R_moveInput) && IsMoveR)
        {

        }
#if UNITY_EDITOR
        // No XR controllers (no headset, no simulator): drive with keyboard + mouse instead.
        if (!_rightController.isValid && !_leftController.isValid)
            DesktopDebugInput();
#endif
        bool primaryHeld = R_primaryValue || L_primaryValue;
        PrimaryHeld = primaryHeld;
        PrimaryDown = primaryHeld && !primaryHeldLastFrame;
        primaryHeldLastFrame = primaryHeld;
        if (PrimaryDown) primaryPressedAt = Time.time;

        if (RestartAble)
        {
            if(R_primaryValue || L_primaryValue || R_secondary || L_secondary || R_gripValue || L_gripValue || R_triggerValue || L_triggerValue || IsMoveL || IsMoveR)
            {
                //SceneManager.LoadScene(2);
                if (SaveManager.instance != null)
                {
                    if (SaveManager.instance.a.time <= 0)
                    {
                        Destroy(SaveManager.instance);
                        SceneManager.LoadScene("Startup Menu_New");
                    }
                    else
                    {
                        SceneManager.LoadScene("Main Scene");
                    }
                }
                else
                {
                    Destroy(SaveManager.instance);
                    SceneManager.LoadScene("Startup Menu_New");
                }
            }
        }

    }
    public void BornFromWater()
    {
        // Random.Range(int, int) excludes the upper bound, so Count (not Count - 1) can pick the
        // last container; with none filled at the start there is nowhere to be born, so stay put.
        if (WC.Count == 0) return;
        int ran = Random.Range(0, WC.Count);
        Vector3 pos = WC[ran].transform.position;
        pos.y += .45f;
        Detach();
        Teleport(pos);
    }
    public bool checkreturn,asd, returnValue;
    public bool onclick(bool Bool)
    {
        if (Bool && checkreturn)
        {
            returnValue = true;
            checkreturn = false;
        }
        else if (Bool && !checkreturn)
        {
            returnValue = false;
            checkreturn = false;
        }
        else if (!Bool)
        {
            returnValue = false;
            checkreturn = true;
        }
        return returnValue;

    }
    public void Move(Vector2 direction)
    {
        if (canmove)
        {
            // A mosquito flies where it looks, so forward keeps the head's pitch instead of being
            // flattened to the horizon. Strafe stays level, so head roll cannot drag you up or down.
            Vector3 forward = LookDirection(mainCamera.transform);
            Vector3 right = mainCamera.transform.right;
            right.y = 0;
            right.Normalize();
            Vector3 moveDirection = (forward * direction.y + right * direction.x).normalized;
            // The right stick stays an explicit up/down thruster, added on top of where you point.
            moveDirection.y += R_moveInput.y * FlyUpSpeed;
            desiredVelocity = moveDirection * Speed;
            CamRot.transform.Rotate(0, R_moveInput.x * CamRotSpeed, 0);
        }
        else
        {
            desiredVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// The head's forward with the dead zone taken out of its pitch and the rest scaled by
    /// <see cref="PitchInfluence"/>. Always unit length, including when looking straight up or down.
    /// </summary>
    private Vector3 LookDirection(Transform head)
    {
        Vector3 f = head.forward;
        Vector3 flat = new Vector3(f.x, 0f, f.z);
        if (flat.sqrMagnitude < 0.000001f)
            // Looking straight up or down: the heading is carried by the head's up axis instead.
            flat = new Vector3(head.up.x, 0f, head.up.z) * -Mathf.Sign(f.y);
        flat.Normalize();

        float pitch = Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
        pitch = Mathf.Sign(pitch) * Mathf.Max(0f, Mathf.Abs(pitch) - PitchDeadZone) * PitchInfluence;

        float rad = pitch * Mathf.Deg2Rad;
        return flat * Mathf.Cos(rad) + Vector3.up * Mathf.Sin(rad);
    }

    public void Drink()
    {
        if (Playing)
        {
            bool wasFull = Current_Blood >= Max_Blood;
            Current_Blood = Mathf.Min(Max_Blood, Current_Blood + Time.deltaTime);
            if (!wasFull && Current_Blood >= Max_Blood)
            {
                DrinkBloodParti.Play();
            }
        }
    }
    public void DrinkNectar()
    {
        if (Playing)
        {
            Current_Nec = Mathf.Min(Max_Nec, Current_Nec + Time.deltaTime);
            if(Current_Nec >= Max_Nec)
            {
                ishungry = false;
            }
        }
    }

    private void InitializeInputDevices()
    {

        if (!_rightController.isValid)
        {
            InitializeInputDevice(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right, ref _rightController);
        }
        if (!_leftController.isValid)
        {
            InitializeInputDevice(InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left, ref _leftController);
        }
        if (!_HMD.isValid)
        {
            InitializeInputDevice(InputDeviceCharacteristics.HeadMounted, ref _HMD);
        }

    }

    private void InitializeInputDevice(InputDeviceCharacteristics inputCharacteristics, ref UnityEngine.XR.InputDevice inputDevice)
    {
        List<UnityEngine.XR.InputDevice> devices = new List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(inputCharacteristics, devices);
        if (devices.Count > 0)
        {
            inputDevice = devices[0];
        }
    }

#if UNITY_EDITOR
    // Editor-only desktop controls so Module 1 can be played without a headset.
    // Only runs when no XR controllers exist, so a real headset always wins.
    //   WASD           fly forward/back/strafe   (left stick)
    //   Q / E          fly down / up             (right stick Y)
    //   Left/Right     turn                      (right stick X)
    //   Up/Down arrow  look up / down            (head pitch)
    //   Right mouse    hold and drag to look around
    //   Space          primary button: drink / mate / lay eggs
    //   T (hold)       right trigger: thermal vision
    //   Tab (hold)     left grip: quest UI
    private float desktopYaw, desktopPitch;
    private bool desktopInit;

    private void DesktopDebugInput()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;

        if (!desktopInit)
        {
            desktopYaw = CamRot.transform.localEulerAngles.y;
            desktopInit = true;
            // The scene ships with an active XR Device Simulator whose bindings (Space, Tab, T,
            // WASD, right mouse) collide with these keys, and it cannot drive PlayerMain anyway
            // (it creates Input System devices, not UnityEngine.XR ones). Park it in desktop mode.
            var sim = FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation.XRDeviceSimulator>();
            if (sim != null)
            {
                sim.gameObject.SetActive(false);
                Debug.Log("[PlayerMain] Desktop debug controls active - XR Device Simulator disabled for this session.");
            }
        }

        L_moveInput = new Vector2(
            (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f),
            (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f));
        // Turn is applied here together with mouse pitch (instead of via Move's Rotate)
        // so yawing a pitched CamRot cannot introduce roll.
        float turn = (kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.leftArrowKey.isPressed ? 1f : 0f);
        float lookUp = (kb.upArrowKey.isPressed ? 1f : 0f) - (kb.downArrowKey.isPressed ? 1f : 0f);
        R_moveInput = new Vector2(0f, (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f));
        R_primaryValue = kb.spaceKey.isPressed;
        R_triggerValue = kb.tKey.isPressed;
        L_gripValue = kb.tabKey.isPressed;

        var mouse = UnityEngine.InputSystem.Mouse.current;
        bool look = mouse != null && mouse.rightButton.isPressed;
        Cursor.lockState = look ? CursorLockMode.Locked : CursorLockMode.None;
        Vector2 md = look ? mouse.delta.ReadValue() * 0.15f : Vector2.zero;

        desktopYaw += turn * CamRotSpeed + md.x;
        desktopPitch = Mathf.Clamp(desktopPitch - md.y - lookUp * CamRotSpeed, -80f, 80f);
        CamRot.transform.localRotation = Quaternion.Euler(desktopPitch, desktopYaw, 0f);
    }

    // Live state readout for desktop mode. "Drinking" = riding a person or flower while feeding.
    private void OnGUI()
    {
        if (!desktopInit) return;
        var style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 13 };
        style.normal.textColor = Color.white;
        string text =
            "DESKTOP DEBUG\n" +
            $"Blood {Current_Blood:0.0}/{Max_Blood}   Nectar {Current_Nec:0.0}/{Max_Nec}   Mated: {isMate}   Eggs: {EggLayed}\n" +
            $"In reach: {CurrentInteraction}   Space: {(R_primaryValue ? "HELD" : "-")}   Drinking: {(transform.parent != null ? "YES (locked on)" : "no")}   " +
            $"Thermal(T): {(R_triggerValue ? "on" : "off")}   Can move: {canmove}\n" +
            "WASD fly | Q/E down/up | arrows look | RMB drag look | Space drink/mate/lay | T thermal | Tab quests";
        GUI.Box(new Rect(10, 10, 640, 84), text, style);
    }
#endif
}
