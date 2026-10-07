using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class MainMenuButtons : MonoBehaviour, IPointerDownHandler
{
    public int level; //0 is quit
    public LevelManager lm;

    private void Start()
    {
        lm = LevelManager.Instance;
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        if (level == 0)
            Application.Quit();
        else
        {
            lm.currentLevel = level;
            SceneManager.LoadScene("ChasmCascadeLevel");
        }
    }
}
