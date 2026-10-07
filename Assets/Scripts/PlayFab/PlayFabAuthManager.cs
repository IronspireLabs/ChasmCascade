using PlayFab.ClientModels;
using PlayFab;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Net.Mail;
using System;

public class PlayFabAuthManager : MonoBehaviour
{
    [Header("Input Credentials (Set via UI or Inspector)")]
    private bool isRequestInFlight = false;
    [SerializeField] private InputField pfemail;
    [SerializeField] private InputField pfUsername;
    [SerializeField] private InputField pfPassword;
    [SerializeField] Text messageLog;

    private void Start()
    {
        if (pfemail != null && PlayerPrefs.HasKey("Email"))
            pfemail.text = PlayerPrefs.GetString("Email");

        if (pfUsername != null && PlayerPrefs.HasKey("Username"))
            pfUsername.text = PlayerPrefs.GetString("Username");
    }

    private void UpdateMessageLog(string message)
    {
        messageLog.text = message;
    }

    // --- REGISTER NEW ACCOUNT ---
    public void RegisterAccount()
    {
        string email = pfemail.text;
        string username = pfUsername.text;
        string password = pfPassword.text;
        if (isRequestInFlight)
        {
            Debug.Log("Request already in progress. Please wait...");
            return;
        }
        if (username != "" && password != "" && email != "")
        {
            if (!IsValidPassword(password))
                return;
            if (!IsValidEmail(email))
            {
                UpdateMessageLog("Email address not valid!");
                return;
            }
            if (CheckIfLoggedIn())
                return;
            isRequestInFlight = true;
            var request = new RegisterPlayFabUserRequest
            {
                Username = username,
                Email = email,
                Password = password,
                RequireBothUsernameAndEmail = true // Ensures unique username & email
            };

            PlayFabClientAPI.RegisterPlayFabUser(request, OnRegisterSuccess, OnPlayFabError);
        }
        else
        {
            UpdateMessageLog("Username, Password and Email cannot be empty!");
        }
    }

    private void OnRegisterSuccess(RegisterPlayFabUserResult result)
    {
        isRequestInFlight = true;
        UpdateMessageLog($"Account Created! Welcome {result.Username} (PlayFab ID: {result.PlayFabId})");

        PlayerPrefs.SetString("Username", $"{ result.Username}");
        PlayerPrefs.SetString("Email", $"{pfemail.text}");
        PlayerPrefs.Save();

        // Save initial default data for the new account
        SaveInitialPlayerData();
        PlayerManager.instance.ClaimSession(false);
        PlayerManager.instance.LoginToLobby(pfUsername.text);
    }
    public bool CheckIfLoggedIn()
    {
        if (PlayFabClientAPI.IsClientLoggedIn())
        {
            UpdateMessageLog($"{pfUsername.text} is already logged in!");
            return true;
        }
        else
        {
            Debug.Log("Player is NOT logged in.");
            return false;
        }
    }

    // --- LOGIN EXISTING ACCOUNT ---
    public void LoginAccount()
    {
        string username = pfUsername.text;
        string password = pfPassword.text;
        // Note: You can log in with Username or Email. 
        // Use LoginWithEmailAddressRequest if accepting email input instead.

        // Prevent making a new request if one is already waiting for a response
        if (isRequestInFlight)
        {
            Debug.Log("Request already in progress. Please wait...");
            return;
        }

        if (username != "" && password != "")
        {
            if(!IsValidPassword(password))
                return;


            isRequestInFlight = true;

            AttemptLogin(username, password);

           //var request = new LoginWithPlayFabRequest
            //{
              //  Username = username,
                //Password = password
            //};
            //PlayFabClientAPI.LoginWithPlayFab(request, OnLoginSuccess, OnLoginError);
        }
        else
        {
            UpdateMessageLog("Username and Password cannot be empty!");
        }
    }
    public void AttemptLogin(string username, string password)
    {
        var request = new LoginWithPlayFabRequest
        {
            Username = username,
            Password = password,
            // Request ReadOnly data on login to check active session status immediately
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetUserData = true,
                UserDataKeys = new List<string> { "IsLoggedIn", "LastHeartbeat" }
            }
        };

        PlayFabClientAPI.LoginWithPlayFab(request, OnLoginSuccess, OnLoginError);
    }
    public bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            var mailAddress = new MailAddress(email);
            // Ensures the address parsed matches the exact input string without trailing characters
            return mailAddress.Address == email;
        }
        catch (FormatException)
        {
            return false;
        }
    }
    public bool IsValidPassword(string password)
    {
        if (password.Length < 6)
        {
            UpdateMessageLog("Password must be atleast six character long!");
            return false;
        }
        if (password.Length > 100)
        {
            UpdateMessageLog("Password cannot be more than 100 characters long!");
            return false;
        }
        if (string.IsNullOrWhiteSpace(password))
        {

            UpdateMessageLog("Password cannot contain blank spaces!");
            return false;
        }
        return true;
    }
    private void OnLoginSuccess(LoginResult result)
    {
        // Fetch standard UserData from the payload
        var userData = result.InfoResultPayload?.UserData;
        string currentDeviceId = SystemInfo.deviceUniqueIdentifier;

        // Check if the IsLoggedIn key exists and is explicitly "true"
        if (userData != null && userData.TryGetValue("IsLoggedIn", out var isLoggedInData) && isLoggedInData.Value == "true")
        {
            string activeDeviceId = userData.TryGetValue("ActiveDeviceId", out var deviceData) ? deviceData.Value : "";

            // If it's a DIFFERENT device, check if its heartbeat is still recent
            if (activeDeviceId != currentDeviceId)
            {
                if (userData.TryGetValue("LastHeartbeat", out var heartbeatData))
                {
                    if (System.DateTime.TryParse(heartbeatData.Value, out System.DateTime lastHeartbeat))
                    {
                        double secondsSinceHeartbeat = (System.DateTime.UtcNow - lastHeartbeat).TotalSeconds;

                        // If heartbeat was sent less than 45 seconds ago by another device -> BLOCK
                        if (secondsSinceHeartbeat < 45)
                        {
                            UpdateMessageLog("Account active on another device.");
                            isRequestInFlight = false; // Reset flag
                            PlayFabClientAPI.ForgetAllCredentials();
                            return;
                        }
                    }
                }
            }
            else
            {
                Debug.Log("<color=yellow>Same device re-entry detected.</color> Overwriting active state.");
            }
        }

        // If IsLoggedIn was "false", missing, or expired -> Claim session!
        isRequestInFlight = false; // Reset flag
        UpdateMessageLog($"Login Successful! Welcome back {pfUsername.text}!");
        PlayerPrefs.SetString("Username", $"{pfUsername.text}");
        PlayerPrefs.Save();
        PlayerManager.instance.username = pfUsername.text;
        PlayerManager.instance.ClaimSession(true);
    }
    private void OnLoginError(PlayFabError error)
    {
        isRequestInFlight = false; // Reset flag
        // 1. Check if the user/account does NOT exist
        if (error.Error == PlayFabErrorCode.InvalidUsernameOrPassword || error.Error == PlayFabErrorCode.AccountNotFound)
        {
            UpdateMessageLog("Incorrect Username or Password.");
            // Example UI update: statusText.text = "Account not found. Please create an account.";
        }
        // 2. Account exists, but password was wrong
        else if (error.Error == PlayFabErrorCode.InvalidParams || error.Error == PlayFabErrorCode.InvalidUsernameOrPassword)
        {
            UpdateMessageLog("Incorrect Username or Password.");
            // Example UI update: statusText.text = "Invalid password. Try again.";
        }
        // 3. Any other network/server issue
        else
        {
            Debug.Log($"Login failed: {error.GenerateErrorReport()}");
        }
    }

    // --- SAVE DATA ---
    private void SaveInitialPlayerData()
    {
        var request = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string>
            {
                { "PlayerScore", "0" },
            }
        };

        PlayFabClientAPI.UpdateUserData(request,
            result => Debug.Log("Default player data initialized."),
            OnPlayFabError);
    }

    // --- LOAD DATA ---
    public void LoadPlayerData()
    {
        var request = new GetUserDataRequest();

        PlayFabClientAPI.GetUserData(request, OnDataLoaded, OnPlayFabError);
    }

    private void OnDataLoaded(GetUserDataResult result)
    {
        if (result.Data != null)
        {
            Debug.Log("--- PLAYER DATA LOADED ---");
            foreach (var item in result.Data)
            {
                Debug.Log($"Key: <b>{item.Key}</b> | Value: <b>{item.Value.Value}</b>");
            }
        }
        else
        {
            Debug.Log("No data found for this player.");
        }
    }

    private void OnPlayFabError(PlayFabError error)
    {
        isRequestInFlight = false;

        switch (error.Error)
        {
            case PlayFabErrorCode.UsernameNotAvailable:
                UpdateMessageLog("Username is already taken.");
                // Example UI update: statusText.text = "Username taken. Try another!";
                break;

            case PlayFabErrorCode.EmailAddressNotAvailable:
                UpdateMessageLog("An account with that email address already exists.");
                break;

            case PlayFabErrorCode.InvalidParams:
                Debug.Log("Invalid parameters provided (e.g., password too short or bad formatting).");
                break;

            default:
                Debug.Log($"PlayFab Error ({error.Error}): {error.GenerateErrorReport()}");
                break;
        }
    }
}
