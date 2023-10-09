using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System;
using System.Net.Sockets;
using System.Threading;
using UnityEngine.SceneManagement;
public class Br_TCP_Server : MonoBehaviour
{

    private SynchronizationContext synchronizationContext;

    public int port = 5000;
    string serverIpAddress = " 192.168.104.17";
    bool createRoomRequested = false;
    Socket newSocket;
    [SerializeField]
    float waitTimeLimit;
    [SerializeField]
    float timer;
    Thread listenClients;
    bool serverActive = false;

    //general buffer to store user incoming data
    byte[] clientData = new Byte[256];
    string roomName = "";

    private static Br_TCP_Server tcpServerInstance;
    private void Awake()
    {

        // Check if an instance already exists
        if (tcpServerInstance != null && tcpServerInstance != this)
        {
            // If an instance already exists, destroy this duplicate GameObject
            Destroy(gameObject);
            return;
        }

        // Set this instance as the singleton
        tcpServerInstance = this;

        // Don't destroy this GameObject when loading new scenes
        DontDestroyOnLoad(gameObject);

        Application.runInBackground = true;
        Br_ICreateRoomUI.OnCreateRoom += CreateRoomRequest;
    }

    void Start()
    {
        if (Screen.fullScreen)
            Screen.fullScreen = false;
    }
    

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();

        if (timer > 0)
        {
            timer -= Time.deltaTime;
        }
        else
        {
            if (listenClients != null && listenClients.IsAlive)
            {
                print("TCP: Waiting has exceeded time limit. Aborting...");
                AbortListenForClients();
            }
        }
    }

    public void CreateRoomRequest()
    {
        if (!enabled) return;

        timer = waitTimeLimit;
        createRoomRequested = true;

        synchronizationContext = SynchronizationContext.Current;
        serverActive = !serverActive;

        //go to hub
        SceneManager.LoadScene("BHub");

        if (serverActive)
        {
            listenClients = new Thread(ListenForClients);
            listenClients.Start();
        }
        else
        {
            AbortListenForClients();
        }
        
    }



    void ListenForClients()
    {
        

        
        try
        {
            print("TCP: Starting Server.");
            //Create and bind socket so that nobody can use it until unbinding
            newSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, port);
            newSocket.Bind(endPoint);
            newSocket.Listen(10);
            print("TCP: Waiting to receive datagrams from client...");

            var client = newSocket.BeginAccept(new AsyncCallback(AcceptClient), null);//blocks the program until receiving a client
            

        }
        catch (System.Exception e)
        {
            Debug.Log("TCP: Connection failed. Error:" + e);
        }


    }

    void AcceptClient(IAsyncResult ar)
    {
        try
        {
            Socket clientSocket = newSocket.EndAccept(ar);
            var clientEp = clientSocket.RemoteEndPoint;
            print("TCP: Connected: " + clientEp.ToString());


            var receiveResult = clientSocket.BeginReceive(clientData, 0, clientData.Length, SocketFlags.None, new AsyncCallback(ReceiveMessage), clientSocket);

            newSocket.BeginAccept(new AsyncCallback(AcceptClient), null);

            
           

        }
        catch (System.Exception e)
        {
            Debug.Log("TCP: Accep Cliend Failed. Error:" + e);
        }
    }

    void ReceiveMessage(IAsyncResult ar)
    {
        try
        {
            Socket clientSocket = (Socket)ar.AsyncState;
            int bytesRead = clientSocket.EndReceive(ar);

            if (bytesRead > 0)
            {
                
                byte[] receivedData = new byte[bytesRead];

                //copy data from general user data buffer to single data buffer to use
                Array.Copy(clientData, receivedData, bytesRead);

                if (serverActive)
                {
                    //post function to be executed in main thread
                    synchronizationContext.Post(_ => HandleReceivedData(receivedData), null);

                    print("TCP: Message Received");


                    //send client a response
                    string response = "Welcome to " + roomName;
                    print("TCP: Sending response: " + response);

                    byte[] responseBytes = System.Text.Encoding.UTF8.GetBytes(response);
                    clientSocket.Send(responseBytes);

                    //keep receiving data from the client
                    clientSocket.BeginReceive(clientData, 0, clientData.Length, SocketFlags.None, new AsyncCallback(ReceiveMessage), clientSocket);

                }

            }
            else
            {
                print("TCP: Client disconnected: " + clientSocket.RemoteEndPoint.ToString());
                clientSocket.Close();

            }

        }
        catch (System.Exception e)
        {
            Debug.Log("TCP: Receive Message Failed. Error:" + e);
        }
    }



    void AbortListenForClients()
    {
        if (listenClients != null)
            listenClients.Abort();
        if (newSocket != null && newSocket.IsBound) newSocket.Close();
        serverActive = false;

    }
    private void OnDisable()
    {
        if (listenClients != null)
            listenClients.Abort();

        if (newSocket != null && newSocket.IsBound) newSocket.Close();
        serverActive = false;
    }


    //Executed in main thread

    //Multiuse that acts when data is received
    void HandleReceivedData(byte[] msg)
    {

        InvokeCreateMessage(msg);
    }

    //Spawn floating text action
    void InvokeCreateMessage(byte[] msg)
    {
        //decode data
        string message = System.Text.Encoding.UTF8.GetString(msg);
        Br_IServer.OnSendMessageToClient?.Invoke(message);
    }
    

    public void SetRoomName(string roomName)
    {
        this.roomName = roomName;
    }

    public string GetRoomName()
    {
        return roomName;
    }

}