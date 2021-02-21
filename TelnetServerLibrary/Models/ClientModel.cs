#nullable enable
using System;
using System.Net;
using TelnetServerLibrary.Models;


namespace TelnetServerLibrary
{
    internal sealed class ClientModel : IClientModel
    {
        #region Class fields
        /// <summary>
        /// Client's identifier.
        /// </summary>
        public uint ClientID { get; private set; }

        /// <summary>
        /// Connection datetime.
        /// </summary>
        internal DateTime ConnectTime { get; private set; }

        /// <summary>
        /// Last time client had activity.
        /// </summary>
        internal DateTime LastActivity { get; private set; }

        public uint LoginAttempts { get; set; }

        public short UserNo { get; set; }
        #endregion

        /// <summary>
        /// Initialize a new instance of the <see cref="ClientModel"/> class.
        /// </summary>
        /// <param name="clientId">The client's identifier.</param>
        /// <param name="remoteAddress">The remote address.</param>
        internal ClientModel(uint clientId, IPEndPoint remoteAddress)
        {
            ClientID = clientId;
            RemoteAddress = remoteAddress;
            ConnectTime = DateTime.Now;
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
        public CLIENT_STATUS CurrentStatus { get; set; }

        /// <summary>
        /// Get or set the client's last received data.
        /// </summary>
        internal string ReceivedData { get; set; } = string.Empty;

        /// <summary>
        /// Append a string to the client's last received data.
        /// </summary>
        /// <param name="dataToAppend">The data to append.</param>
        internal void AppendReceivedData(string dataToAppend)
        {
            ReceivedData += dataToAppend;
            LastActivity = DateTime.Now;
        }

        /// <summary>
        /// Remove the last character from the client's last received data.
        /// </summary>
        internal void RemoveLastCharacterReceived()
        {
            ReceivedData = ReceivedData[0..^1];
            LastActivity = DateTime.Now;
        }

        public override string ToString()
        {
            string ip = string.Format($"{RemoteAddress.Address}:{RemoteAddress.Port}");
            string res = string.Format($"Client #{ClientID} (From: {ip}, Status: {CurrentStatus}, Connected at: {ConnectTime})");
            return res;
        }
    }
}