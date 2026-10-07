using PlayFab.ClientModels;
using PlayFab;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SaveAndLoadTestLiftLogger : MonoBehaviour
{
    public string testVariable;

    [SerializeField] InputField testIF;

    private void Start()
    {
        LoadTestVariable();   
    }
    public void LoadTestVariable()
    {
        if (!PlayFabClientAPI.IsClientLoggedIn()) return;

        var request = new GetUserDataRequest
        {
            Keys = new List<string> { "TestVariable" }
        };

        PlayFabClientAPI.GetUserData(request,
            result =>
            {
                if (result.Data == null) return;

                if (result.Data.TryGetValue("TestVariable", out var testVar))
                    testVariable = testVar.Value;


                //testIF.text = testVariable;
                Debug.Log("<color=green>Variable successfully loaded!</color>");

                // 2. Tell the server to set our authoritative network health

            },
            error => Debug.LogError($"Failed to load data: {error.GenerateErrorReport()}")
        );
    }
    public void SaveTestVariable()
    {
        testVariable = testIF.text;
        var request = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string>
            {
                { "TestVariable", testVariable } // Key must match what you load in GetUserData
            },
            // Optional: Choose permission level
            // Permission = UserDataPermission.Public // Default is Private
        };

        PlayFabClientAPI.UpdateUserData(request, OnSaveSuccess, OnSaveFailure);
    }

    private void OnSaveSuccess(UpdateUserDataResult result)
    {
        Debug.Log("Successfully saved TestVariable to PlayFab!");
    }

    private void OnSaveFailure(PlayFabError error)
    {
        Debug.LogError($"Failed to save data: {error.GenerateErrorReport()}");
    }
    public void Logout()
    {

    }

    private void OnApplicationQuit()
    {
       // PlayerManager.instance.LogoutFirstPlayer();
    }
}
