using System;
using System.Net;
using TelnetServerLibrary;
using TelnetServerLibrary.Models;

namespace TelnetServer
{
    public class Program
    {
        private static readonly ITelnetServer s_server = new Server(IPAddress.Any);

        static void Main(string[] args)
        {
            s_server.ClientConnected += ClientConnected;
            s_server.ClientDisconnected += ClientDisconnected;
            s_server.ConnectionBlocked += ConnectionBlocked;
            s_server.MessageReceived += MessageReceived;

            s_server.Start();

            Console.WriteLine($"SERVER STARTED AT: {DateTime.Now} (IP {IPAddress.Any})");
            Console.WriteLine("Type 'Q' to quit or 'B' to broadcast a message.");

            ConsoleKey read = ConsoleKey.NoName;

            do
            {
                if (read == ConsoleKey.B)
                {
                    Console.WriteLine("Enter broadcast message:");
                    s_server.SendMessageToAll(Console.ReadLine());
                }
            } while ((read = Console.ReadKey(true).Key) != ConsoleKey.Q);

            Console.WriteLine($"SERVER STOPPED AT {DateTime.Now}");
            s_server.Stop();
            s_server.Dispose();
        }

        private static void ClientConnected(object sender, IClientModel client)
        {
            Console.WriteLine("CONNECTED: " + client);
            s_server.SendMessageToClient(client, $"Telnet Server{Server.CRLF}Login: ");
        }

        private static void ClientDisconnected(object sender, IClientModel client) => Console.WriteLine("DISCONNECTED: " + client);

        private static void ConnectionBlocked(object sender, IPEndPoint? endPoint) =>
            Console.WriteLine(string.Format($"BLOCKED: {endPoint?.Address}:{endPoint?.Port} at {DateTime.Now}"));

        private static void MessageReceived(object sender, MessageReceivedEventArgs args)
        {
            if (args.ClientInstance.CurrentStatus != CLIENT_STATUS.LOGGED_IN)
            {
                HandleLogin(args.ClientInstance, args.ReceivedData);
                return;
            }

            Console.WriteLine("MESSAGE: " + args.ReceivedData);

            switch (args.ReceivedData)
            {
                case "q":
                case "quit":
                case "lo":
                case "logout":
                case "exit":
                    s_server.KickClient(args.ClientInstance);
                    break;
                case "clear":
                    s_server.ClearClientScreen(args.ClientInstance);
                    s_server.SendMessageToClient(args.ClientInstance, Server.CURSOR);
                    break;
                default:
                    s_server.SendMessageToClient(args.ClientInstance, Server.CRLF + Server.CURSOR);
                    break;
            }
        }

        private static void HandleLogin(IClientModel client, string message)
        {
            if (client.CurrentStatus == CLIENT_STATUS.GUEST)
            {
                if (message.Equals("root"))
                {
                    s_server.SendMessageToClient(client, Server.CRLF + "Password: ");
                    client.CurrentStatus = CLIENT_STATUS.AUTHENTICATING;
                }
                else
                    s_server.KickClient(client);
            }
            else if (client.CurrentStatus == CLIENT_STATUS.AUTHENTICATING)
            {
                if (message.Equals("r00t"))
                {
                    s_server.ClearClientScreen(client);
                    s_server.SendMessageToClient(client, $"Successfully authenticated. There are {s_server.ConnectionCount - 1} other users online.");
                    s_server.SendMessageToClient(client, Server.CRLF + Server.CURSOR);
                    client.CurrentStatus = CLIENT_STATUS.LOGGED_IN;
                }
                else
                    s_server.KickClient(client);
            }
        }
    }
}