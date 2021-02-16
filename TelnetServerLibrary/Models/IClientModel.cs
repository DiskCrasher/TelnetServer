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

    public interface IClientModel
    {
        uint ClientID { get; }
        IPEndPoint RemoteAddress { get; }
        CLIENT_STATUS CurrentStatus { get; set; }
        string ToString();
    }
}