#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;
using TelnetServerLibrary.Models;

namespace TelnetServerLibrary
{
    public class Server : ITelnetServer
    {
        #region Class fields
        public const string CRLF = "\r\n";

        /// <summary>
        /// End of line constant.
        /// </summary>
        public const string CURSOR = "> ;";

        // Telnet constants.
        private const int TELNET_IAC = 0xff; // Telnet Interpret As Command byte.
        private const int TELNET_DO = 0xfd;
        private const int TELNET_WILL = 0xfb;
        private const int TELNET_ECHO = 0x01;                // RFC 857
        private const int TELNET_SUPPRESS_GO_AHEAD = 0x03;   // RFC 858
        private const int TELNET_TOGGLE_FLOW_CONTROL = 0x21; // RFC 1080

        public event EventHandler<IClientModel> ClientConnected, ClientDisconnected;
        public event EventHandler<IPEndPoint> ConnectionBlocked;
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        private bool m_alreadyDisposed;

        /// <summary>
        /// Server's main listening socket.
        /// </summary>
        private readonly Socket m_serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        /// <summary>
        /// Client inactivity timer.
        /// </summary>
        private readonly Timer m_timeoutTimer = new Timer(TimeSpan.FromMinutes(1).TotalMilliseconds) { AutoReset = true };

        /// <summary>
        /// IP address on which to listen.
        /// </summary>
        private readonly IPAddress m_ip;

        /// <summary>
        /// Port to listen on.
        /// </summary>
        private readonly int m_port;

        /// <summary>
        /// Default data size for received data.
        /// </summary>
        private readonly int m_dataSize;

        /// <summary>
        /// Received data.
        /// </summary>
        private readonly byte[] m_data;

        /// <summary>
        /// All connected clients indexed by their socket.
        /// </summary>
        private readonly Dictionary<Socket, ClientModel> m_clients = new Dictionary<Socket, ClientModel>();

        public bool AcceptConnections { get; private set; } = true;

        public int ClientInactivityTimeout { get; set; } = 15;

        public int ConnectionCount => m_clients.Count;

        public int MaxConnections { get; set; } = 100;
        #endregion

        /// <summary>
        /// Initialize a new instance of the <see cref="Server"/> class.
        /// </summary>
        /// <param name="ip">The IP on which to listen to.</param>
        /// <param name="dataSize">Data size for received data.</param>
        public Server(IPAddress ip, int port = 23, int dataSize = 128)
        {
            m_ip = ip;
            m_port = port;
            m_dataSize = dataSize;
            m_data = new byte[dataSize];
            m_timeoutTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
        }

        /// <summary>
        /// Start the server.
        /// </summary>
        public void Start()
        {
            m_serverSocket.Bind(new IPEndPoint(m_ip, m_port));
            m_serverSocket.Listen(0);
            m_serverSocket.BeginAccept(new AsyncCallback(HandleIncomingConnection), m_serverSocket);
            m_timeoutTimer.Start();
        }

        /// <summary>
        /// Clear the screen for the specified client.
        /// </summary>
        /// <param name="c">The client on which
        /// to clear the screen.</param>
        public void ClearClientScreen(IClientModel c) => SendMessageToClient(c, "\u001B[1J\u001B[H");

        /// <summary>
        /// Send a text message to the specified client.
        /// </summary>
        /// <param name="c">The client.</param>
        /// <param name="message">The message.</param>
        public void SendMessageToClient(IClientModel c, string? message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Socket? clientSocket = GetSocketByClient(c);
                SendMessageToSocket(clientSocket, message);
            }
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
                    ClientModel client = m_clients[s];

                    if (client.CurrentStatus == CLIENT_STATUS.LOGGED_IN)
                    {
                        SendMessageToSocket(s, CRLF + message + CRLF + CURSOR);
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
        private ClientModel? GetClientBySocket(Socket? clientSocket)
        {
            ClientModel? client = null;
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
        private Socket? GetSocketByClient(IClientModel client)
        {
            Socket? s = m_clients.FirstOrDefault(x => x.Value.ClientID == client.ClientID).Key;
            return s;
        }

        /// <summary>
        /// Kick the specified client from the server.
        /// </summary>
        /// <param name="client">The client.</param>
        public void KickClient(IClientModel client)
        {
            SendMessageToClient(client, CRLF + "You are being logged off. Goodbye!");
            CloseSocket(GetSocketByClient(client));
            ClientDisconnected(this, client); // Fire event.
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

                if (AcceptConnections)
                {
                    Socket newSocket = oldSocket.EndAccept(result);

                    var clientID = (uint)m_clients.Count + 1;
                    var client = new ClientModel(clientID, (IPEndPoint)newSocket.RemoteEndPoint!);
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
                    ClientConnected(this, client);
                    m_serverSocket.BeginAccept(new AsyncCallback(HandleIncomingConnection), m_serverSocket);
                }
                else
                {
                    ConnectionBlocked(this, (IPEndPoint?)oldSocket.RemoteEndPoint);
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
                    ClientModel? client = GetClientBySocket(clientSocket);
                    string receivedData = client!.ReceivedData;

                    // 0x2E = '.', 0x0D = carriage return, 0x0A = new line
                    if ((m_data[0] == 0x2E && m_data[1] == 0x0D && receivedData.Length == 0) || (m_data[0] == 0x0D && m_data[1] == 0x0A))
                    {
                        //sendMessageToSocket(clientSocket, "\u001B[1J\u001B[H");
                        MessageReceived(this, new MessageReceivedEventArgs { ClientInstance = client, ReceivedData = client.ReceivedData });
                        client.ReceivedData = string.Empty;
                    }
                    else
                    {
                        if (m_data[0] == 0x08) // 0x08 => backspace character
                        {
                            if (receivedData.Length > 0)
                            {
                                client.RemoveLastCharacterReceived();
                                SendBytesToSocket(clientSocket, new byte[] { 0x08, 0x20, 0x08 });
                            }
                            else
                                clientSocket?.BeginReceive(m_data, 0, m_dataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
                        }
                        else if (m_data[0] == 0x7F) // 0x7F => delete character
                            clientSocket?.BeginReceive(m_data, 0, m_dataSize, SocketFlags.None, new AsyncCallback(ReceiveData), clientSocket);
                        else
                        {
                            client.AppendReceivedData(Encoding.ASCII.GetString(m_data, 0, bytesReceived));

                            // Echo back the received character if client is not typing a password.
                            if (client.CurrentStatus != CLIENT_STATUS.AUTHENTICATING)
                                SendBytesToSocket(clientSocket, new byte[] { m_data[0] });
                            else // Echo back asterisks if client is typing a password.
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

        private void OnTimedEvent(object source, ElapsedEventArgs e)
        {
            foreach (var client in m_clients)
                if (DateTime.Now - client.Value.LastActivity > TimeSpan.FromMinutes(ClientInactivityTimeout))
                    KickClient(client.Value);

            AcceptConnections = m_clients.Count <= MaxConnections;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_alreadyDisposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    m_serverSocket?.Dispose();
                    m_timeoutTimer.Elapsed -= OnTimedEvent;
                    m_timeoutTimer.Dispose();
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