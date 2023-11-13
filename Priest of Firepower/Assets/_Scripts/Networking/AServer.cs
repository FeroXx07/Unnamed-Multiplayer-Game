
#define AUTHENTICATION_CODE
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace _Scripts.Networking
{

    public class ClientManager
    {
        private int _nextClientId = 0;

        public int GetNextClientId()
        {
            int clientId = _nextClientId;
            _nextClientId++;
            return clientId;
        }
    }
    public class AServer : GenericSingleton<AServer>
    {
        #region variables
        IPEndPoint _endPoint;

        // It's used to signal to an asynchronous operation that it should stop or be interrupted.
        // Cancellation tokens are particularly useful when you want to stop an ongoing operation due to user input, a timeout,
        // or any other condition that requires the operation to terminate prematurely.

        Process _listenConnectionProcess = new Process();
        private List<Process> _authenticationProcessList = new List<Process>();

        ClientManager _clientManager;

        private List<Process> _clientThreads = new List<Process>();
        private List<ClientData> _clientList = new List<ClientData>();
        private List<ClientData> _clientListToRemove = new List<ClientData>();

        //actions
        Action<int> _onClientAccepted;
        Action _onClientRemoved;
        Action<int> _onClientDisconnected;
        Action<byte[]> _onDataRecieved;

        //handeles connection with clients
        Socket _serverTcp;
        Socket _serverUDP;

        ServerAuthenticator _authenticator = new ServerAuthenticator();

        struct Process
        {
            public Thread Thread;
            public CancellationTokenSource cancellationToken;
        }

        private bool _isServerInitialized  = false;
        #endregion

        public AServer(IPEndPoint endPoint)
        {
            _endPoint = endPoint;
        }


        #region client data
        class ClientData
        {
            public int ID = -1;
            public string Username = "";
            public ClientMetadata MetaData;
            public ClientSate State;
            public Socket ConnectionTcp;
            public Socket ConnectionUDP;
            public CancellationTokenSource AuthenticationToken; //if disconnection request invoke cancellation token to shutdown all related processes
            public bool IsHost = false;
        }
        struct ClientMetadata
        {
            public int Port;
            public IPAddress IP;
            //add time stamp
        }
        public enum ClientSate
        {
            CONNECTED,
            AUTHENTICATED,
            IN_GAME
        }
        #endregion

        #region enable/disable functions
        private void OnDisable()
        {

            Debug.Log("Stopping server ...");

            StopAuthenticationThread();

            DisconnectAllClients();

            StopAllClientThreads();

            Debug.Log("Closing server connection ...");

            if (_serverTcp.Connected)
            {
                _serverTcp.Shutdown(SocketShutdown.Both);
            }
            _serverTcp.Close();
        }
        private void Update()
        {
            RemoveDisconectedClient();
        }
        #endregion

        #region helper funcitons
        void StopAuthenticationThread()
        {
            foreach(Process p in _authenticationProcessList)
            {
                p.cancellationToken.Cancel();
                if (p.Thread != null)
                {
                    if (p.Thread.IsAlive)
                    {
                        p.Thread.Join();
                    }
                    //make sure it is not alive
                    if (p.Thread.IsAlive)
                    {
                        p.Thread.Abort();
                    }
                }
            }
 
        }

        void StopConnectionListener()
        {
            _listenConnectionProcess.cancellationToken.Cancel();
            if (_listenConnectionProcess.Thread.IsAlive)
                _listenConnectionProcess.Thread.Join();
            if (_listenConnectionProcess.Thread.IsAlive)
                _listenConnectionProcess.Thread.Abort();
        }
        void DisconnectAllClients()
        {
            foreach (ClientData client in _clientList)
            {
                RemoveClient(client);
            }
        }
        void StopAllClientThreads()
        {

            Debug.Log("Server: Waiting for all threads to terminate.");
            foreach (Process p in _clientThreads)
            {
                p.cancellationToken.Cancel();
                if (p.Thread.IsAlive)
                    p.Thread.Join();
            }
            foreach (Process p in _clientThreads)
            {
                if (p.Thread.IsAlive)
                    p.Thread.Abort();
            }
        }
        void RemoveDisconectedClient()
        {
            if (_clientListToRemove.Count > 0)
            {
                lock (_clientList)
                {
                    foreach (ClientData clientToRemove in _clientListToRemove)
                    {
                        _clientList.Remove(clientToRemove);
                    }
                }
                Debug.Log("removed " + _clientListToRemove.Count + " clients");
                _clientListToRemove.Clear();
            }
        }
        #endregion

        #region getter setter funtions
        public bool GetServerInit() { return _isServerInitialized; }
        #endregion

        #region core functions
        public void InitServer()
        {
            _clientManager = new ClientManager();
            //start server
            Debug.Log("Starting server ...");
            //create listener tcp
            _serverTcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            //create end point
            //If the port number doesn't matter you could pass 0 for the port to the IPEndPoint.
            //In this case the operating system (TCP/IP stack) assigns a free port number for you.
            //So for the ip any it listens to all directions ipv4 local LAN and 
            //also the public ip. TOconnect from the client use any of the ips
            if(_endPoint == null)
                _endPoint = new IPEndPoint(IPAddress.Any, NetworkManager.Instance.connectionAddress.port);

            //bind to ip and port to listen to
            _serverTcp.Bind(_endPoint);
            // Using ListenForConnections() method we create 
            // the Client list that will want
            // to connect to Server
            _serverTcp.Listen(4);


            _listenConnectionProcess.cancellationToken = new CancellationTokenSource();
            _listenConnectionProcess.Thread = new Thread(() => StartConnectionListener(_listenConnectionProcess.cancellationToken.Token));
            _listenConnectionProcess.Thread.Start();

            _isServerInitialized = true;
        }

        public void SendToAll(byte[] data)
        {
            Debug.Log("Boradcasting message ...");
            foreach(ClientData client in _clientList)
            {
                client.ConnectionUDP.SendTo(data, data.Length, SocketFlags.None, _endPoint);
            }
        }
        public  void SendCriticalToAll(byte[] data)
        {
            foreach (ClientData client in _clientList)
            {
                client.ConnectionTcp.SendTo(data, data.Length, SocketFlags.None, _endPoint);
            }
        }
        public void SendToClient(int clientId, byte[] data)
        {
            foreach (ClientData client in _clientList)
            {
                if (client.ID == clientId)
                {
                    client.ConnectionUDP.SendTo(data,data.Length, SocketFlags.None, _endPoint);

                    return;
                }
            }
        }
        void StartConnectionListener(CancellationToken token)
        {
            try
            {
                while(!token.IsCancellationRequested)
                {
                    Socket incomingConnection = _serverTcp.Accept();
                    IPEndPoint IpEndPoint = incomingConnection.LocalEndPoint as IPEndPoint;
                    //if not local host

                    Debug.Log("Socket address: " + IpEndPoint.Address + " local address:" + IPAddress.Loopback);
                    if (IpEndPoint.Address.Equals(IPAddress.Loopback))
                    {
                        Debug.Log("Server : host connected ... ");
                        CreateClient(incomingConnection, "Host");
                    }
                    else
                    {                        
                        Process authenticate = new Process();

                        authenticate.cancellationToken = new CancellationTokenSource();
                        authenticate.Thread = new Thread(() => Authenticate(incomingConnection, authenticate.cancellationToken.Token));
                        authenticate.Thread.Start();
                    }                    
                }

            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }  
        void HandleClient(ClientData clientData)
        {
            Debug.Log("Starting Client Thread " + clientData.ID + " ...");
            try
            {
                while (!clientData.AuthenticationToken.IsCancellationRequested)
                {

                    if (clientData.ConnectionTcp.Available > 0)
                    {
                        ReceiveSocketData(clientData.ConnectionTcp);
                    }
                    if (clientData.ConnectionUDP.Available > 0)
                    {
                        ReceiveSocketData(clientData.ConnectionUDP);
                    }
                    // Add some delay to avoid busy-waiting
                    Thread.Sleep(10);
                }
            }
            catch (SocketException se)
            {
                if (se.SocketErrorCode == SocketError.ConnectionReset ||
                    se.SocketErrorCode == SocketError.ConnectionAborted)
                {
                    // Handle client disconnection (optional)
                    Debug.Log($"Client {clientData.ID} disconnected: {se.Message}");
                    RemoveClient(clientData);
                }
                else
                {
                    // Handle other socket exceptions
                    Debug.Log($"SocketException: {se.SocketErrorCode}, {se.Message}");
                }
            }
            catch (Exception e)
            {
                // Handle other exceptions
                Debug.Log($"Exception: {e.Message}");
            }
        }
        void ReceiveSocketData(Socket socket)
        {
            try
            {
                lock(NetworkManager.Instance.IncomingStreamLock)
                {
                    byte[] buffer = new byte[1500];

                    Debug.Log("Server: waiting for authentication ... ");
                    // Receive data from the client
                    socket.Receive(buffer);
                    Debug.Log("Server: recieved data ... ");
                    MemoryStream stream = new MemoryStream(buffer);

                    NetworkManager.Instance.AddIncomingDataQueue(stream);
                }
            }
            catch (SocketException se)
            {
               // Handle other socket exceptions
               Debug.Log($"SocketException: {se.SocketErrorCode}, {se.Message}");

            }
            catch (Exception e)
            {
                // Handle other exceptions
                Debug.Log($"Exception: {e.Message}");
            }
        }
        int CreateClient(Socket clientSocket, string userName)
        {
            lock (_clientList)
            {
                ClientData clientData = new ClientData();

                clientData.ID = _clientManager.GetNextClientId();
                clientData.Username = userName;

                clientData.ConnectionTcp = clientSocket;
                //add a time out exeption for when the client disconnects or has lag or something
                clientData.ConnectionTcp.ReceiveTimeout = 1000;
                clientData.ConnectionTcp.SendTimeout = 1000;

                //create udp connection
                clientData.ConnectionUDP = new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);
                clientData.ConnectionUDP.Bind(clientSocket.RemoteEndPoint);
                clientData.ConnectionUDP.ReceiveTimeout = 100;
                clientData.ConnectionUDP.SendTimeout = 100;

                //store endpoint
                IPEndPoint clientEndPoint = (IPEndPoint)clientSocket.RemoteEndPoint;
                clientData.MetaData.IP = clientEndPoint.Address;
                clientData.MetaData.Port = clientEndPoint.Port;

                clientData.AuthenticationToken = new CancellationTokenSource();

                _clientList.Add(clientData);

                Debug.Log("Connected client Id: " + clientData.ID);


                //Create the handeler of the chat for that client
                //create a hole thread to recive important data from server-client
                //like game state, caharacter selection, map etc

                Process clientProcess = new Process();

                clientProcess.cancellationToken = new CancellationTokenSource();

                clientProcess.Thread = new Thread(() => HandleClient(clientData));

                clientProcess.Thread.IsBackground = true;
                clientProcess.Thread.Name = clientData.ID.ToString();
                clientProcess.Thread.Start();



                _clientThreads.Add(clientProcess);

                return clientData.ID;
            }
        }
        void RemoveClient(ClientData clientData)
        {
            // Remove the client from the list of connected clients    
            lock (_clientList)
            {
                // Cancel the client's cancellation token source
                clientData.AuthenticationToken.Cancel();

                // Close the client's socket
                if (clientData.ConnectionTcp.Connected)
                {
                    clientData.ConnectionTcp.Shutdown(SocketShutdown.Both);
                }
                clientData.ConnectionTcp.Close();

                _clientListToRemove.Add(clientData);

                Debug.Log("Client " + clientData.ID + " disconnected.");
            }
        }
        #endregion

        #region Authentication
        void Authenticate( Socket incomingSocket, CancellationToken cancellationToken)
        {
            incomingSocket.ReceiveTimeout = 2000;
            Debug.Log("Server: Authentication process started ... ");
            try
            {
                //accept new Connections ... 
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (incomingSocket.Available > 0)
                    {
                        Debug.Log("Authentication message recieved ...");
                        ReceiveSocketData(incomingSocket);
                    }
                    Thread.Sleep(100);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Thread.ResetAbort();
                Debug.Log("Shutting down authentication process ...");
            }
            finally
            {
            }
        }

        public ServerAuthenticator GetAuthenticator() { return _authenticator; }
        #endregion
    }
}
