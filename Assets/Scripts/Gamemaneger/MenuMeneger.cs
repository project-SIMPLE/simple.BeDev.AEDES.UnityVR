using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuMeneger : MonoBehaviour
{
    public void M1(int s)
    {
        SceneManager.LoadScene(s);
    }
}
