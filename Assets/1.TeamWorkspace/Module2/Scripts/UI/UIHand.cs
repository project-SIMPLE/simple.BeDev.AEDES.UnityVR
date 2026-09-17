using UnityEngine;

public class UIHand : MonoBehaviour
{
    public static UIHand Instance { get; private set; }
    [Header("Reference")]
    public GameObject uiHand;
    public GameObject hand;
    public GameObject SwatterPrefab;
    public GameObject handSwatterPoint;

    [Header("Setting")]
    public LayerMask layerMaskHand;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void Start()
    {

    }

    public void Update()
    {
        ShowUIHanD();
    }

    public void ShowUIHanD()
    {
        if (Physics.Raycast(transform.position, transform.up, out RaycastHit hit, 10f, layerMaskHand))
        {
            uiHand.SetActive(true);
        }
        else
        {
            uiHand.SetActive(false);
        }

        Debug.DrawRay(transform.position, transform.up);
    }

    public void SpawnSwatter()
    {
        if (FindAnyObjectByType<SwatterHand>() == null)
        {
            GameObject swatter = Instantiate(SwatterPrefab, handSwatterPoint.transform.position, handSwatterPoint.transform.rotation);
            swatter.transform.SetParent(handSwatterPoint.transform);
        }
    }
}
