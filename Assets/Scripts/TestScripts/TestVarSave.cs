using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class LiftLoggerTestVarSave : MonoBehaviour, IPointerDownHandler
{
    public int buttonTab;

    public void OnPointerDown(PointerEventData eventData)
    {
        if(buttonTab == 0)
        {
            SceneManager.LoadScene("LevelSelect");
            //PlayerManager.instance.LogoutFirstPlayer();
            //PlayerManager.instance.LogoutToLogin();
        }
        if(buttonTab == 1)
        {
            SceneManager.LoadScene("ChasmCascadeLevel");
        }
    }
}
