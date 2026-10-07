using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class LobbyClicks : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] int lobbyButton; //0 = host, 1 = client
    [SerializeField] Lobby lobby;
    [SerializeField] PlayFabAuthManager playFabManager;

    public bool playFab;
    public string sceneName;
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if(!playFab)
            {
                if (lobbyButton == 0)
                    lobby.OnClickHost();
                else if (lobbyButton == 1)
                    lobby.OnClickClient();
                else if (lobbyButton == 2)
                {
                    Application.Quit();
                    Quit();
                }
                else if (lobbyButton == 3)
                {
                    PlayerManager.instance.LogoutFirstPlayer();
                    SceneManager.LoadScene($"{sceneName}");
                }
            }
            else
            {
                if (lobbyButton == 0)
                    playFabManager.RegisterAccount();
                else if (lobbyButton == 1)
                    playFabManager.LoginAccount();
                else if (lobbyButton == 2)
                {
                    Application.Quit();
                    Quit();
                }
            }
        }
    }
    public void Quit()
    {
#if UNITY_STANDALONE
        Application.Quit();
#endif
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
