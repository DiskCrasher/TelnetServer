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
        event EventHandler<IClientModel> ClientConnected;
        event EventHandler<IClientModel> ClientDisconnected;
        event EventHandler<IPEndPoint> ConnectionBlocked;
        event EventHandler<MessageReceivedEventArgs> MessageReceived;

        bool AcceptConnections { get; }
        int ConnectionCount { get; }
        void ClearClientScreen(IClientModel client);
        void KickClient(IClientModel client);
        void SendMessageToAll(string? message);
        void SendMessageToClient(IClientModel client, string? message);
        void Start();
        void Stop();
    }
}
