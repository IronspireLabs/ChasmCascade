using PlayFab.ClientModels;
using PlayFab;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager instance;

    public bool host;

    public string ipAddress;
    public string username;

    public string messageLog;

    public string lobbyScene;
    public string loginScene;

    public string mySessionId;
    private bool hasClaimedSession = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
            Destroy(gameObject);
    }
    public void LoginToLobby(string playerUsername)
    {
        username = playerUsername;
        SceneManager.LoadScene(lobbyScene);
        messageLog = "Host or join a session.";
    }
    public void LogoutToLogin()
    {
        SceneManager.LoadScene(loginScene);
        messageLog = "Log in or create an account.";

    }
    public void ClaimSession(bool isLogin)
    {
        mySessionId = System.Guid.NewGuid().ToString();

        var updateDataRequest = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string>
            {
                { "IsLoggedIn", "true" },
                { "LastHeartbeat", System.DateTime.UtcNow.ToString("o") }
            }
        };

        PlayFabClientAPI.UpdateUserData(updateDataRequest,
            res =>
            {
                Debug.Log("<color=green>Session claimed!</color> First player is logged in.");
                hasClaimedSession = true; // Mark that this client is actively logged in
                // Start sending heartbeats every 30 seconds to keep the session alive
                CancelInvoke(nameof(SendHeartbeat)); // Prevent duplicate invokes
                InvokeRepeating(nameof(SendHeartbeat), 30f, 30f);
            },
            err => Debug.LogError(err.GenerateErrorReport())
        );

        if(isLogin)
            LoginToLobby(username);
    }
    private void SendHeartbeat()
    {
        var updateRequest = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string>
            {
                { "LastHeartbeat", System.DateTime.UtcNow.ToString("o") }
            }
        };
        PlayFabClientAPI.UpdateUserData(updateRequest, null, null);
    }
    // Call this when the first player manually logs out or quits the application
    public void LogoutFirstPlayer()
    {
        if (!PlayFabClientAPI.IsClientLoggedIn() || !hasClaimedSession)
        {
            Debug.Log("Logout skipped: No active session logged in on this client.");
            return;
        }

        CancelInvoke(nameof(SendHeartbeat));
        hasClaimedSession = false; // Reset local state
        var updateDataRequest = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string>
            {
                { "IsLoggedIn", "false" }
            }
        };

        PlayFabClientAPI.UpdateUserData(updateDataRequest,
            res =>
            {
                PlayFabClientAPI.ForgetAllCredentials();
                Debug.Log("Logged out cleanly. Session released.");
            },
            err => PlayFabClientAPI.ForgetAllCredentials()
        );
    }
    private void OnApplicationQuit()
    {
        LogoutFirstPlayer();
    }
}
