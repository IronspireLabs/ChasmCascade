using PlayFab.ClientModels;
using PlayFab;
using System.Collections.Generic;
using UnityEngine;

public class PlayFabTest : MonoBehaviour
{
    [Header("Test Data")]
    [SerializeField] private int testIntegerValue = 42;

    private void Start()
    {
        // Step 1: Log in automatically with a unique local device ID
        Login();
    }

    private void Login()
    {
        var request = new LoginWithCustomIDRequest
        {
            CustomId = SystemInfo.deviceUniqueIdentifier,
            CreateAccount = true
        };

        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnPlayFabError);
    }

    private void OnLoginSuccess(LoginResult result)
    {
        Debug.Log("Successfully logged into PlayFab! PlayFab ID: " + result.PlayFabId);

        // Step 2: Save the integer once login completes
        SaveIntegerData("PlayerScore", testIntegerValue);
    }

    private void SaveIntegerData(string key, int value)
    {
        var request = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string>
            {
                { key, value.ToString() }
            }
        };

        PlayFabClientAPI.UpdateUserData(request, result =>
        {
            Debug.Log($"Successfully saved integer value ({testIntegerValue})!");

            // Step 2: Once save succeeds, retrieve it back to test the read operation
            LoadIntegerData("PlayerScore");

        }, OnPlayFabError);
    }
    private void LoadIntegerData(string key)
    {
        var request = new GetUserDataRequest
        {
            // You can leave Keys null to retrieve ALL player data, 
            // or specify key names to fetch only what you need:
            Keys = new List<string> { key }
        };

        PlayFabClientAPI.GetUserData(request, OnDataLoadSuccess, OnPlayFabError);
    }
    private void OnDataLoadSuccess(GetUserDataResult result)
    {
        string keyToFind = "PlayerScore";

        // Check if the key exists in the returned dictionary
        if (result.Data != null && result.Data.ContainsKey(keyToFind))
        {
            string rawValue = result.Data[keyToFind].Value;

            // Parse string back into an integer
            if (int.TryParse(rawValue, out int loadedInteger))
            {
                Debug.Log($"<color=green>SUCCESS!</color> Retrieved '{keyToFind}' from PlayFab: <b>{loadedInteger}</b>");
            }
            else
            {
                Debug.LogWarning($"Found key '{keyToFind}', but value '{rawValue}' could not be parsed as an int.");
            }
        }
        else
        {
            Debug.LogWarning($"Key '{keyToFind}' was not found in PlayFab User Data.");
        }
    }

    private void OnPlayFabError(PlayFabError error)
    {
        Debug.LogError("PlayFab Error: " + error.GenerateErrorReport());
    }
}
