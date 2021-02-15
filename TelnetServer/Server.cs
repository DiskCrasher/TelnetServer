using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;


namespace TelnetServer
{
    public delegate void ConnectionEventHandler(Client client);
    public delegate void ConnectionBlockedEventHandler(IPEndPoint? endPoint);
    public delegate void MessageReceivedEventHandler(Client client, string message);

    public class Server : IDisposable
    {
        #region Class fields
        /// <summary>
        /// Telnet default port.
        /// </summary>
        private const int PORT = 23;

        /// <summary>
        /// End of line constant.
        /// </summary>
        public const string CURSOR = " > ";
        private const int TELNET_IAC = 0xff; // Telnet Interpret As Command byte.
        private const int TELNET_DO = 0xfd;
        private const int TELNET_WILL = 0xfb;
        private const int TELNET_ECHO = 0x01;                // RFC 857
        private const int TELNET_SUPPRESS_GO_AHEAD = 0x03;   // RFC 858
        private const int TELNET_TOGGLE_FLOW_CONTROL = 0x21; // RFC 1080

        private bool m_alreadyDisposed;

        /// <summary>
        /// Server's main listening socket.
        /// </summary>
        private readonly Socket m_serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        /// <summary>
        /// IP on which to listen.
        /// </summary>
        private readonly IPAddress m_ip;

        /// <summary>
        /// Default data size for received data.
        /// </summary>
        private readonly int m_dataSize;

        /// <summary>
        /// Received data.
        /// </summary>
        private readonly byte[] m_data;

        /// <summary>
        /// <see langword="true"/> for allowing incoming connections;
        /// <see langword="false"/> otherwise.
        /// </summary>
        private bool m_acceptIncomingConnections;

        /// <summary>
        /// All connected clients indexed by their socket.
        /// </summary>
        private readonly Dictionary<Socket, Client> m_clients = new Dictionary<Socket, Client>();

        /// <summary>
        /// Occurs when a client is connected.
        /// </summary>
        public event ConnectionEventHandler ClientConnected;

        /// <summary>
        /// Occurs when a client is disconnected.
        /// </summary>
        public event ConnectionEventHandler ClientDisconnected;

        /// <summary>
        /// Occurs when an incoming connection is blocked.
        /// </summary>
        public event ConnectionBlockedEventHandler ConnectionBlocked;

        /// <summary>
        /// Occurs when a message is received.
        /// </summary>
        public event MessageReceivedEventHandler MessageReceived;
        #endregion

        /// <summary>
        /// Initialize a new instance of the <see cref="Server"/> class.
        /// </summary>
        /// <param name="ip">The IP on which to listen to.</param>
        /// <param name="dataSize">Data size for received data.</param>
        public Server(IPAddress ip, int dataSize = 1024)
        {
            m_ip = ip;
            m_dataSize = dataSize;
            m_data = new byte[dataSize];
            m_acceptIncomingConnections = true;
        }

        /// <summary>
        /// Start the server.
        /// </summary>
        public void Start()
        {
            m_serverSocket.Bind(new IPEndPoint(m_ip, PORT));
            m_serverSocket.Listen(0);
            m_serverSocket.BeginAccept(new AsyncCallback(HandleIncomingConnection), m_serverSocket);
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        public void Stop() => m_serverSocket.Close();

        /// <summary>
        /// Return whether incoming connections are allowed.
        /// </summary>
        /// <returns>True is connections are allowed;
        /// false otherwise.</returns>
        public bool IncomingConnectionsAllowed() => m_acceptIncomingConnections;

        /// <summary>
        /// Deny incoming connections.
        /// </summary>
        public void DenyIncomingConnections() => m_acceptIncomingConnections = false;

        /// <summary>
        /// Allow the incoming connections.
        /// </summary>
        public void AllowIncomingConnections() => m_acceptIncomingConnections = true;

        /// <summary>
        /// Clear the screen for the specified client.
        /// </summary>
        /// <param name="c">The client on which
        /// to clear the screen.</param>
        public void ClearClientScreen(Client c) => SendMessageToClient(c, "\u001B[1J\u001B[H");

        /// <summary>
        /// Send a text message to the specified client.
        /// </summary>
        /// <param name="c">The client.</param>
        /// <param name="message">The message.</param>
        public void SendMessageToClient(Client c, string message)
        {
            Socket? clientSocket = GetSocketByClient(c);
            SendMessageToSocket(clientSocket, message);
        }

        /// <summary>
        /// Send a text message to the specified socket.
        /// </summary>
        /// <param name="s">The socket.</param>
        /// <param name="message">The message.</param>
        private void SendMessageToSocket(Socket? s, string message)
        {
            byte[] data = Encoding.ASCII.GetBytes(message);
            SendBytesToSocket(s, data);
        }

        /// <summary>
        /// Send bytes to the specified socket.
        /// </summary>
        /// <param name="s">The socket.</param>
        /// <param name="data">The bytes.</param>
        private void SendBytesToSocket(Socket? s, byte[] data) =>
            _ = s?.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(SendData), s);

        /// <summary>
        /// Send a message to all connected clients.
        /// </summary>
        /// <param name="message">The message.</param>
        public void SendMessageToAll(string? message)
        {
            if (string.IsNullOrEmpty(message)) return;

            foreach (Socket s in m_clients.Keys)
                try
                {
                    Client client = m_clients[s];

                    if (client.CurrentStatus == CLIENT_STATUS.LOGGED_IN)
                    {
                        SendMessageToSocket(s, Environment.NewLine + message + Environment.NewLine + CURSOR);
                        client.ReceivedData = string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    m_clients.Remove(s);
                }
        }

        /// <summary>
        /// Get the client by socket.
        /// </summary>
        /// <param name="clientSocket">The client's socket.</param>
        /// <returns>If the socket is found, the client instance
        /// is returned; otherwise <see langword="null"/> is returned.</returns>
        private Client? GetClientBySocket(Socket? clientSocket)
        {
            Client? client = null;
            if (clientSocket != null)
                _ = m_clients.TryGetValue(clientSocket, out client);
            return client;
        }

        /// <summary>
        /// Get the socket by client.
        /// </summary>
        /// <param name="client">The client instance.</param>
        /// <returns>If the client is found, the socket is
        /// returned; otherwise null is returned.</returns>
        private Socket? GetSocketByClient(Client client)
        {
            Socket? s = m_clients.FirstOrDefault(x => x.Value.ClientID == client.ClientID).Key;
            return s;
        }

        /// <summary>
        /// Kick the specified client from the server.
        /// </summary>
        /// <param name="client">The client.</param>
        public void KickClient(Client client)
        {
            CloseSocket(GetSocketByClient(client));
            ClientDisconnected(client);
        }

        /// <summary>
        /// Close the socket and remove the client from the clients list.
        /// </summary>
        /// <param name="clientSocket">The client socket.</param>
        private void CloseSocket(Socket? clientSocket)
        {
            if (clientSocket != null)
            {
                clientSocket.Close();
                m_clients.Remove(clientSocket);
            }
        }

        /// <summary>
        /// Handle an incoming connection. If incoming connections are allowed,
        /// the client is added to the clients list and triggers the client connected event.
        /// Else, the connection blocked event is triggered.
        /// </summary>
        private void HandleIncomingConnection(IAsyncResult result)
        {
            try
            {
                Socket? oldSocket = (Socket?)result.AsyncState;
                if (oldSocket == null) return;

                if (m_acceptIncomingConnections)
                {
                    Socket newSocket = oldSocket.EndAccept(result);

                    var clientID = (uint)m_clients.Count + 1;
                    var client = new Client(clientID, (IPEndPoint)newSocket.RemoteEndPoint!);
                    m_clients.Add(newSocket, client);

                    SendBytesToSocket(
                        newSocket,
                        new byte[] {
                            TELNET_IAC, TELNET_DO, TELNET_ECHO,                  // Do Echo
                            TELNET_IAC, TELNET_DO, TELNET_TOGGLE_FLOW_CONTROL,   // Do Remote Flow Control
                            TELNET_IAC, TELNET_WILL, TELNET_ECHO,                // Will Echo
                            TELNET_IAC, TELNET_WILL, TELNET_SUPPRESS_GO_AHEAD,   // Will Supress Go Ahead
                        }
                    );

                    client.ReceivedData = string.Empty;
                    ClientConnected(client);
                    m_serverSocket.BeginAccept(new AsyncCallback(HandleIncomingConnection), m_serverSocket);
                }
                else
                {
                    ConnectionBlocked((IPEndPoint?)oldSocket.RemoteEndPoint);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Send data to a socket.
        /// </summary>
        private void SendData(IAsyncResult result)
        {
            try
            {
                Socket? clientSocket = (Socket?)result.AsyncState;
                clientSocket?.EndSend(result);
                clientSocket?.BeginReceive(m_data, 0, m_dataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Receive and process data from a socket. Triggers the message received event in
        /// case the client pressed the return key.
        /// </summary>
        private void ReceiveData(IAsyncResult result)
        {
            try
            {
                Socket? clientSocket = (Socket?)result.AsyncState; // Socket may be closed/disposed.
                int bytesReceived = clientSocket?.Connected ?? false ? clientSocket?.EndReceive(result) ?? 0 : 0;

                if (bytesReceived == 0)
                {
                    CloseSocket(clientSocket);
                    m_serverSocket.BeginAccept(new AsyncCallback(HandleIncomingConnection), m_serverSocket);
                }
                else if (m_data[0] < 0xF0)
                {
                    Client? client = GetClientBySocket(clientSocket);
                    string receivedData = client!.ReceivedData;

                    // 0x2E = '.', 0x0D = carriage return, 0x0A = new line
                    if ((m_data[0] == 0x2E && m_data[1] == 0x0D && receivedData.Length == 0) || (m_data[0] == 0x0D && m_data[1] == 0x0A))
                    {
                        //sendMessageToSocket(clientSocket, "\u001B[1J\u001B[H");
                        MessageReceived(client, client.ReceivedData);
                        client.ReceivedData = string.Empty;
                    }
                    else
                    {
                        // 0x08 => backspace character
                        if (m_data[0] == 0x08)
                        {
                            if (receivedData.Length > 0)
                            {
                                client.RemoveLastCharacterReceived();
                                SendBytesToSocket(clientSocket, new byte[] { 0x08, 0x20, 0x08 });
                            }
                            else
                                clientSocket?.BeginReceive(m_data, 0, m_dataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
                        }
                        // 0x7F => delete character
                        else if (m_data[0] == 0x7F)
                            clientSocket?.BeginReceive(m_data, 0, m_dataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
                        else
                        {
                            client.AppendReceivedData(Encoding.ASCII.GetString(m_data, 0, bytesReceived));

                            // Echo back the received character
                            // if client is not writing any password
                            if (client.CurrentStatus != CLIENT_STATUS.AUTHENTICATING)
                                SendBytesToSocket(clientSocket, new byte[] { m_data[0] });

                            // Echo back asterisks if client is
                            // writing a password
                            else
                                SendMessageToSocket(clientSocket, "*");

                            clientSocket?.BeginReceive(m_data, 0, m_dataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
                        }
                    }
                }
                else
                    clientSocket?.BeginReceive(m_data, 0, m_dataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_alreadyDisposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    m_serverSocket?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                m_alreadyDisposed = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Server()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}