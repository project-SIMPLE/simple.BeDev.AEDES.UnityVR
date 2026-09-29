using UnityEngine;

public class Mosquito : MonoBehaviour
{
    public float flySpeed = 5f;
    public float randomChangeInterval = 2f;
    public float maxHeight = 10f;
    public float minHeight = 0f;
    public Vector3 maxDirection = new Vector3(1f, 1f, 1f);
    public float spawnBoundaryRadius = 1f;

    [Header("Biting (master design B2/B4: 10 bites ends the pilot's stint)")]
    [Tooltip("Distance at which this mosquito starts steering toward the player.")]
    public float attractRadius = 4f;
    [Tooltip("How strongly it homes in once inside attractRadius.")]
    [Range(0f, 1f)] public float seekStrength = 0.55f;
    [Tooltip("Distance at which it lands a bite.")]
    public float biteDistance = 0.35f;
    [Tooltip("Seconds before the same mosquito can bite again.")]
    public float biteCooldown = 4f;
    [Tooltip("Seconds after emerging before a mosquito can bite. They emerge just above open containers, i.e. in the face of a player who is covering one.")]
    public float firstBiteDelay = 3f;

    private Vector3 _randomDirection;
    private int _wallMask;
    private float _directionChangeTimer;
    private float _nextBiteAt;

    // Camera.main walks the scene graph, and there can be fifty of these alive at once, so the head
    // transform is resolved once and shared.
    private static Transform s_head;
    private static int s_headFrame = -1;

    private static Transform Head()
    {
        if (s_headFrame == Time.frameCount) return s_head;
        s_headFrame = Time.frameCount;
        if (s_head == null && Camera.main != null) s_head = Camera.main.transform;
        return s_head;
    }

    void Start()
    {
        _wallMask = LayerMask.GetMask("Wall");
        _nextBiteAt = Time.time + Mathf.Max(firstBiteDelay, 0f);
        GenerateRandomDirection();
    }

    void Update()
    {
        FlyRandomly();
    }

    public void FlyRandomly()
    {
        _directionChangeTimer -= Time.deltaTime;

        if (_directionChangeTimer <= 0f)
        {
            GenerateRandomDirection();
            _directionChangeTimer = randomChangeInterval;
        }

        SeekAndBite();
        BounceOffWalls();

        transform.position += _randomDirection * (flySpeed * Time.deltaTime);

        Vector3 clampedPosition = transform.position;
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, minHeight, maxHeight);
        transform.position = clampedPosition;

        if (_randomDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(_randomDirection);
    }

    // The swarm has to actually be a threat: B2's Scene 4 ends the stint at 10 bites, and nothing in
    // the module bit the player at all before this.
    private void SeekAndBite()
    {
        var manager = M2Manager.Instance;
        if (manager == null || !manager.IsPlaying) return;

        var head = Head();
        if (head == null) return;

        Vector3 toHead = head.position - transform.position;
        float distance = toHead.magnitude;
        if (distance > attractRadius || distance < 0.0001f) return;

        // Home in, but keep some wander so the swarm does not become a laser-guided cloud.
        _randomDirection = Vector3.Slerp(_randomDirection, toHead / distance, seekStrength * Time.deltaTime * 3f).normalized;

        if (distance > biteDistance || Time.time < _nextBiteAt) return;

        _nextBiteAt = Time.time + Mathf.Max(biteCooldown, 0.5f);
        manager.NoteBite();

        // Peel away after feeding so one mosquito cannot pin the player.
        _randomDirection = (-toHead / distance + Random.insideUnitSphere * 0.4f).normalized;
        _directionChangeTimer = randomChangeInterval;
    }

    // Reflect off the wall we are about to fly into. The previous version raycast along four fixed
    // axes, computed a reflection, and then unconditionally overwrote it with a fresh random
    // direction on the next line - so the bounce never took effect and mosquitoes drifted straight
    // through the house. There is no Rigidbody here, so nothing else stops them.
    private void BounceOffWalls()
    {
        if (Physics.Raycast(transform.position, _randomDirection, out RaycastHit hit, spawnBoundaryRadius, _wallMask))
        {
            _randomDirection = Vector3.Reflect(_randomDirection, hit.normal).normalized;
            _directionChangeTimer = randomChangeInterval;
        }

        Debug.DrawRay(transform.position, _randomDirection * spawnBoundaryRadius, Color.green);
    }

    private void GenerateRandomDirection()
    {
        Vector3 direction = new Vector3(
            Random.Range(-maxDirection.x, maxDirection.x),
            Random.Range(-maxDirection.y, maxDirection.y),
            Random.Range(-maxDirection.z, maxDirection.z)
        );

        _randomDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Swatter"))
        {
            Destroy(gameObject);
        }
    }
}
