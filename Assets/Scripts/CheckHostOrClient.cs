using PurrNet;
using PurrNet.Transports;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CheckHostOrClient : MonoBehaviour
{
    [SerializeField] private string scene;

    private void Awake()
    {
        if(PlayerManager.instance.host)
        {
            NetworkManager.main.StartHost();
        }
        else
        {
            if (NetworkManager.main.transport is UDPTransport updTransport)
            {
                updTransport.address = PlayerManager.instance.ipAddress;
                NetworkManager.main.StartClient();
            }
        }
    }
    private void OnEnable()
    {
        if(NetworkManager.main != null)
        {
            NetworkManager.main.onClientConnectionState += OnConnectionStateChanged;
        }
    }
    private void OnDisable()
    {
        if (NetworkManager.main != null)
        {
            NetworkManager.main.onClientConnectionState -= OnConnectionStateChanged;
        }
    }
    public void OnConnectionStateChanged(ConnectionState state)
    {
        Debug.Log($"Connected state changed: {state}");

        if(state == ConnectionState.Connected)
        {

        }
        if(state == ConnectionState.Disconnected)
        {
            PlayerManager.instance.messageLog = "Host not found, please check the IP and try again or host your own session.";
            SceneManager.LoadScene(scene);
        }
    }
}
