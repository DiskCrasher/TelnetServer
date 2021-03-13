using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace TelnetServerLibrary.Models
{
    public enum CLIENT_STATUS
    {
        /// <summary>
        /// Unauthenticated client.
        /// </summary>
        GUEST,
        /// <summary>
        /// Client is authenticating.
        /// </summary>
        AUTHENTICATING,
        /// <summary>
        /// Client is logged in.
        /// </summary>
        LOGGED_IN,
        /// <summary>
        /// Client is a new user.
        /// </summary>
        NEW_USER,
    }

    public interface IClientModel
    {
        uint ClientID { get; }
        CLIENT_STATUS CurrentStatus { get; set; }
        uint LoginAttempts { get; set; }
        IPEndPoint RemoteAddress { get; }
        string ToString();
        short UserNo { get; set; }
    }
}