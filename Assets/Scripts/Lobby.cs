using PurrNet;
using PurrNet.Transports;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Lobby : MonoBehaviour
{
    [SerializeField] private InputField ipUsername;
    [SerializeField] private InputField ipPassword;
    [SerializeField] private string scene;
    [SerializeField] private InputField ipInputField;
    [SerializeField] private string defaultIP = "127.0.0.1";
    [SerializeField] private Text message;
    [SerializeField] private Text username;

    private void Start()
    {
        if (ipInputField != null && PlayerPrefs.HasKey("LastJoinedIP"))
            ipInputField.text = PlayerPrefs.GetString("LastJoinedIP");
        /*
        if (ipUsername != null && PlayerPrefs.HasKey("LastJoinedName"))
            ipUsername.text = PlayerPrefs.GetString("LastJoinedName");
        */

        if (PlayerManager.instance.messageLog == "")
            message.text = "Host or join a session.";
        else
            message.text = PlayerManager.instance.messageLog;

        username.text = ($"Welcome {PlayerManager.instance.username}!");
    }
    public void OnClickHost()
    {
        PlayerManager.instance.host = true;
        SceneManager.LoadScene(scene);
    }
    public void OnClickClient()
    {
        string rawInput = ipInputField != null ? ipInputField.text : "";
        string targetIP = string.IsNullOrWhiteSpace(ipInputField.text) ? defaultIP : ipInputField.text.Trim();

        PlayerPrefs.SetString("LastJoinedIP", targetIP);
        PlayerPrefs.Save();

        PlayerManager.instance.host = false;
        PlayerManager.instance.ipAddress = targetIP;
        SceneManager.LoadScene(scene);
    }
    public void OnClickHostOld()
    {
        string username = ipUsername.text;
        string password = ipPassword.text;

        if (username != "" && password != "")
        {
            // 2. Check if the username exists in PlayerPrefs
            if (!PlayerPrefs.HasKey(username))
            {
                message.text = "Creating new account...";
                //Username does not exist, create new user
                PlayerPrefs.SetString("LastJoinedName", ipUsername.text);
                PlayerPrefs.SetString(username, password);
                PlayerPrefs.Save();
                PlayerManager.instance.host = true;
                PlayerManager.instance.username = username;
                SceneManager.LoadScene(scene);
            }
            else
            {
                //Username already exists, check password is correct
                string storedPassword = PlayerPrefs.GetString(username);
                if (password == storedPassword)
                {
                    PlayerPrefs.SetString("LastJoinedName", ipUsername.text);
                    PlayerPrefs.Save();
                    PlayerManager.instance.host = true;
                    PlayerManager.instance.username = username;
                    message.text = "Logging in...";
                    SceneManager.LoadScene(scene);
                }
                else
                {
                    message.text = "This account exists but the password is incorrect.";
                }
            }
        }
        else
        {
            message.text = "Username and password can't be empty.";
        }
    }
    public void OnClickClientOld()
    {
        string username = ipUsername.text;

        if (username != "")
        {
            Debug.Log(username);

            string rawInput = ipInputField != null ? ipInputField.text : "";
            string targetIP = string.IsNullOrWhiteSpace(ipInputField.text) ? defaultIP : ipInputField.text.Trim();

            PlayerPrefs.SetString("LastJoinedName", ipUsername.text);
            PlayerPrefs.SetString("LastJoinedIP", targetIP);
            PlayerPrefs.Save();
            PlayerManager.instance.host = false;
            PlayerManager.instance.ipAddress = targetIP;
            PlayerManager.instance.username = username;
            SceneManager.LoadScene(scene);
        }
        else
        {
            Debug.Log("Username can't be empty!");
        }
    }
}
