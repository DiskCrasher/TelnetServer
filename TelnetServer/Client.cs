using System;
using System.Net;


namespace TelnetServer
{
    public enum CLIENT_STATUS
    {
        /// <summary>
        /// Unauthenticated client.
        /// </summary>
        GUEST = 0,
        /// <summary>
        /// Client is authenticating.
        /// </summary>
        AUTHENTICATING = 1,
        /// <summary>
        /// Client is logged in.
        /// </summary>
        LOGGED_IN = 2
    }

    public class Client
    {
        #region Class fields
        /// <summary>
        /// Client's identifier.
        /// </summary>
        internal uint ClientID { get; set; }

        /// <summary>
        /// Connection datetime.
        /// </summary>
        private readonly DateTime m_connectedAt;
        #endregion

        /// <summary>
        /// Initialize a new instance of the <see cref="Client"/> class.
        /// </summary>
        /// <param name="clientId">The client's identifier.</param>
        /// <param name="remoteAddress">The remote address.</param>
        public Client(uint clientId, IPEndPoint remoteAddress)
        {
            ClientID = clientId;
            RemoteAddress = remoteAddress;
            m_connectedAt = DateTime.Now;
            CurrentStatus = CLIENT_STATUS.GUEST;
            ReceivedData = string.Empty;
        }

        /// <summary>
        /// Get the remote address.
        /// </summary>
        /// <returns>Client's remote address.</returns>
        public IPEndPoint RemoteAddress { get; private set; }

        /// <summary>
        /// Get or set the client's current status.
        /// </summary>
        /// <returns>The client's status.</returns>
        public CLIENT_STATUS CurrentStatus { get; internal set; }

        /// <summary>
        /// Get or set the client's last received data.
        /// </summary>
        public string ReceivedData { get; internal set; } = string.Empty;

        /// <summary>
        /// Append a string to the client's last received data.
        /// </summary>
        /// <param name="dataToAppend">The data to append.</param>
        public void AppendReceivedData(string dataToAppend) => ReceivedData += dataToAppend;

        /// <summary>
        /// Remove the last character from the client's last received data.
        /// </summary>
        public void RemoveLastCharacterReceived() => ReceivedData = ReceivedData[0..^1];

        public override string ToString()
        {
            string ip = string.Format($"{RemoteAddress.Address}:{RemoteAddress.Port}");
            string res = string.Format($"Client #{ClientID} (From: {ip}, Status: {CurrentStatus}, Connected at: {m_connectedAt})");
            return res;
        }
    }
}