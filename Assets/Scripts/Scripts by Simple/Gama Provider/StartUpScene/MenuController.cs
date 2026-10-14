
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    [SerializeField] private string host = "172.20.10.2";
    [SerializeField] private string portWithMiddleware = "8080";
    [SerializeField] private string portWithoutMiddleware = "1000";
    private bool useMiddleWare;
    TextMeshProUGUI textMP;
    Toggle m_Toggle;

    public void Start()
    {
        // A menu may hide the IP text, the middleware toggle or the player id (Module 1's
        // Startup Menu_New has all three switched off). FindGameObjectWithTag skips inactive
        // objects, so each one can be missing; the IP and middleware defaults are still set.
        textMP = Tagged<TextMeshProUGUI>("textIP");
        m_Toggle = Tagged<Toggle>("useMiddleWare");
        TextMeshProUGUI textPN = Tagged<TextMeshProUGUI>("textPN");
        if (textPN != null) textPN.text = "Player id: " + StaticInformation.getId();

        if (!PlayerPrefs.HasKey("MIDDLEWARE") || PlayerPrefs.GetString("MIDDLEWARE").Length == 0)
            PlayerPrefs.SetString("MIDDLEWARE", "N");
        useMiddleWare = PlayerPrefs.GetString("MIDDLEWARE").Equals("Y");
        if (m_Toggle != null) m_Toggle.SetIsOnWithoutNotify(useMiddleWare);

        string port = useMiddleWare ? portWithMiddleware : portWithoutMiddleware;

        string ip = PlayerPrefs.GetString("IP");
        if (ip.Length == 0)
        {
            ip = host;
            PlayerPrefs.SetString("IP", ip);
        }
        if (textMP != null) textMP.text = "Current IP: " + ip + "/" + port;
    }

    static T Tagged<T>(string tag) where T : Component
    {
        GameObject go = GameObject.FindGameObjectWithTag(tag);
        return go != null ? go.GetComponent<T>() : null;
    }

    public void OnValueMiddleWare()
    {
        if (m_Toggle == null) return;
        useMiddleWare = m_Toggle.isOn;
        string port = useMiddleWare ? portWithMiddleware : portWithoutMiddleware;
        if (textMP != null) textMP.text = "Current IP: " + PlayerPrefs.GetString("IP") + ":" + port;

    }
    public void StartBtn()
    {
        PlayerPrefs.SetString("MIDDLEWARE", useMiddleWare ? "Y" : "N") ;
        string port = useMiddleWare ? portWithMiddleware : portWithoutMiddleware;
        PlayerPrefs.SetString("PORT", port);
        SceneManager.LoadScene("Main Scene");
    }

    public void IPBtn()
    {
        SceneManager.LoadScene("IP Menu");
    }
}
