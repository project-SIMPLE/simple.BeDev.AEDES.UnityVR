using UnityEngine;

public class Mosquito : MonoBehaviour
{
    public float flySpeed = 5f;
    public float randomChangeInterval = 2f;
    public float maxHeight = 10f;
    public float minHeight = 0f;
    public Vector3 maxDirection = new Vector3(1f, 1f, 1f);
    public float spawnBoundaryRadius = 1f;
    private Vector3 _randomDirection;
    private string _wllLayer = "Wall";
    private float directionChangeTimer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        GenerateRandomDirection();
    }

    // Update is called once per frame
    void Update()
    {
        FlyRandomly();
    }

    public void FlyRandomly()
    {
        directionChangeTimer -= Time.deltaTime;

        if (directionChangeTimer <= 0f)
        {
            GenerateRandomDirection();
            directionChangeTimer = randomChangeInterval;
        }

        transform.position += _randomDirection * flySpeed * Time.deltaTime;

        Vector3 clampedPosition = transform.position;
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, minHeight, maxHeight);
        
        transform.position = clampedPosition;

        Vector3 pointLook = transform.position + _randomDirection;
        transform.LookAt(pointLook);
    }

    private void GenerateRandomDirection()
    {
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hitInfo, spawnBoundaryRadius, LayerMask.GetMask(_wllLayer)))
        {
            _randomDirection = Vector3.Reflect(_randomDirection, hitInfo.normal);
        }

        if (Physics.Raycast(transform.position, transform.right, out RaycastHit hitInfoRight, spawnBoundaryRadius, LayerMask.GetMask(_wllLayer)))
        {
            _randomDirection = Vector3.Reflect(_randomDirection, hitInfoRight.normal);
        }

        if (Physics.Raycast(transform.position, -transform.right, out RaycastHit hitInfoLeft, spawnBoundaryRadius, LayerMask.GetMask(_wllLayer)))
        {
            _randomDirection = Vector3.Reflect(_randomDirection, hitInfoLeft.normal);
        }

        if (Physics.Raycast(transform.position, -transform.forward, out RaycastHit hitInfoUp, spawnBoundaryRadius, LayerMask.GetMask(_wllLayer)))
        {
            _randomDirection = Vector3.Reflect(_randomDirection, hitInfoUp.normal);
        }

        Debug.DrawRay(transform.position, transform.forward * spawnBoundaryRadius, Color.green);
        Debug.DrawRay(transform.position, transform.right * spawnBoundaryRadius, Color.green);
        Debug.DrawRay(transform.position, -transform.forward * spawnBoundaryRadius, Color.green);
        Debug.DrawRay(transform.position, -transform.right * spawnBoundaryRadius, Color.green);

        _randomDirection = new Vector3(
            Random.Range(-maxDirection.x, maxDirection.x),
            Random.Range(-maxDirection.y, maxDirection.y),
            Random.Range(-maxDirection.z, maxDirection.z)
        ).normalized;
    }
<<<<<<< HEAD

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Swatter"))
        {
            Destroy(gameObject);
        }
    }

=======
   
    
>>>>>>> 54c0e92 (update module2)
}
