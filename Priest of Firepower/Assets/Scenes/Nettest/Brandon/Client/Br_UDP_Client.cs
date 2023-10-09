using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using TMPro;
using UnityEngine.SceneManagement;

public class Br_UDP_Client : MonoBehaviour
{
    private SynchronizationContext synchronizationContext;

    public int serverPort;


    [SerializeField]
    float waitTimeLimit;
    [SerializeField]
    float timer;


    Thread connectToServerThread;
    Thread recieveResponseThread;
    Socket newSocket;
    EndPoint serverEndpoint;

    private static Br_UDP_Client udpClientInstance;
    string username;
    string serverIp;
    bool connectedToServer = false;
    private void Awake()
    {
        // Check if an instance already exists
        if (udpClientInstance != null && udpClientInstance != this)
        {
            // If an instance already exists, destroy this duplicate GameObject
            Destroy(gameObject);
            return;
        }

        // Set this instance as the singleton
        udpClientInstance = this;

        // Don't destroy this GameObject when loading new scenes
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
        Br_IJoinRoomUI.OnJoinRoom += JoinRoom;
        Br_IServer.OnSendMessageToServer += SendMessageToServer;
        Br_IServer.OnReceiveMessageFromServer += ReceiveMessageFromServer;
    }

    void Start()
    {
        if (Screen.fullScreen)
            Screen.fullScreen = false;
    }
   
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
        if (timer > 0)
        {
            timer -= Time.deltaTime;
        }
        else
        {
            if (connectToServerThread != null && connectToServerThread.IsAlive)
            {
                AbortConnectToServer();
            }
        }
    }

    public void JoinRoom()
    {
        if (!enabled) return;
        try
        {
            print("UDP: conectig to ip: " + this.serverIp);
            string serverIp = this.serverIp.Remove(this.serverIp.Length - 1);

            //Create and bind socket so that nobody can use it until unbinding
            newSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, serverPort);

            string message = username + " joined.";
            byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(message);

            IPAddress ipAddress;
            if (!IPAddress.TryParse(serverIp, out ipAddress))
            {
                // Handle invalid IP address input
                print("UDP: Invalid IP address: " + serverIp);
                return;
            }
            print("UDP: ipAddress: " + ipAddress);

            serverEndpoint = new IPEndPoint(ipAddress, serverPort);

            SceneManager.LoadScene("BHub");


            newSocket.SendTo(messageBytes, serverEndpoint);

            synchronizationContext = SynchronizationContext.Current;
            recieveResponseThread = new Thread(HandleConnectionToServer);
            recieveResponseThread.Start();


        }
        catch (System.Exception e)
        {
            Debug.Log("UDP: Connection failed.. trying again. Error: " + e);
        }
    }

    void SendMessageToServer(string message)
    {
        byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
        newSocket.SendTo(messageBytes, serverEndpoint);

    }

    void ReceiveMessageFromServer(string message)
    {
        byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
        newSocket.SendTo(messageBytes, serverEndpoint);

    }


    void HandleConnectionToServer()
    {
        byte[] response = new byte[256];
        int responseByteCount = newSocket.ReceiveFrom(response, response.Length, SocketFlags.None, ref serverEndpoint);
        if (responseByteCount > 0)
        {
            synchronizationContext.Post(_ => InvokeCreateResponse(response), null);
            string message = System.Text.Encoding.UTF8.GetString(response);
            print(message);
            connectedToServer = true;
            KeepListeningToServer();
        }
    }

    void KeepListeningToServer()
    {
        if (connectedToServer)
        {
            KeepListeningToServer();
        }
        newSocket.Close();
    }


    void InvokeCreateResponse(byte[] msg)
    {
        //decode data
        string message = System.Text.Encoding.UTF8.GetString(msg);
        Br_IServer.OnSendMessageToServer?.Invoke(message);

    }

    void AbortConnectToServer()
    {
        print("UDP: Waiting has exceeded time limit. Aborting...");
        if (connectToServerThread != null)
            connectToServerThread.Abort();
        if (newSocket != null && newSocket.IsBound) newSocket.Close();
    }
    private void OnDisable()
    {
        if (connectToServerThread != null)
            connectToServerThread.Abort();
        if (newSocket != null && newSocket.IsBound) newSocket.Close();
    }

    //todo: quitar unused connectToServer thread;

    public void SetUsername(string username)
    {
        this.username = username;
    }
    public void SetServerIp(string ip)
    {
        this.serverIp = ip;
    }

    public string GetUsername()
    {
        return username;
    }

}