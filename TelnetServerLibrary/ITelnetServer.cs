#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TelnetServerLibrary.Models;

namespace TelnetServerLibrary
{
    public sealed class MessageReceivedEventArgs : EventArgs
    {
        public IClientModel ClientInstance { get; set; }
        public string ReceivedData { get; set; }
    }

    public interface ITelnetServer : IDisposable
    {
        /// <summary>
        /// Fires when a client is connected.
        /// </summary>
        event EventHandler<IClientModel> ClientConnected;

        /// <summary>
        /// Fires when a client is disconnected.
        /// </summary>
        event EventHandler<IClientModel> ClientDisconnected;

        /// <summary>
        /// Occurs when an incoming connection is blocked.
        /// </summary>
        event EventHandler<IPEndPoint> ConnectionBlocked;

        /// <summary>
        /// Occurs when a message is received.
        /// </summary>
        event EventHandler<MessageReceivedEventArgs> MessageReceived;

        /// <summary>
        /// <see langword="true"/> enables incoming socket connections.
        /// </summary>
        bool AcceptConnections { get; }

        /// <summary>
        /// Number of currently connected clients.
        /// </summary>
        int ConnectionCount { get; }

        /// <summary>
        /// Maximum client connection count. When exceeded, new connections will be blocked.
        /// </summary>
        int MaxConnections { get; set; }

        /// <summary>
        /// Max number of idle minutes before client is disconnected.
        /// </summary>
        int ClientInactivityTimeout { get; set; }

        void ClearClientScreen(IClientModel client);
        void KickClient(IClientModel client);
        void SendMessageToAll(string? message);
        void SendMessageToClient(IClientModel client, string? message);
        void Start();
    }
}
