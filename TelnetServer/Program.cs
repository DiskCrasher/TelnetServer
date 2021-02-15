using System;
using System.Net;


namespace TelnetServer
{
    public class Program
    {
        private static readonly Server s_server = new Server(IPAddress.Any);

        static void Main(string[] args)
        {
            s_server.ClientConnected += ClientConnected;
            s_server.ClientDisconnected += ClientDisconnected;
            s_server.ConnectionBlocked += ConnectionBlocked;
            s_server.MessageReceived += MessageReceived;

            s_server.Start();

            Console.WriteLine($"SERVER STARTED ON: {DateTime.Now} (IP {IPAddress.Any})");

            char read = Console.ReadKey(true).KeyChar;

            do
            {
                if (read.Equals('b'))
                    s_server.SendMessageToAll(Console.ReadLine());
            } while ((read = Console.ReadKey(true).KeyChar) != 'q');

            s_server.Stop();
            s_server.Dispose();
        }

        private static void ClientConnected(Client client)
        {
            Console.WriteLine("CONNECTED: " + client);
            s_server.SendMessageToClient(client, $"Telnet Server{Environment.NewLine}Login: ");
        }

        private static void ClientDisconnected(Client client) => Console.WriteLine("DISCONNECTED: " + client);

        private static void ConnectionBlocked(IPEndPoint? endPoint) =>
            Console.WriteLine(string.Format($"BLOCKED: {endPoint?.Address}:{endPoint?.Port} at {DateTime.Now}"));

        private static void MessageReceived(Client client, string message)
        {
            if (client.CurrentStatus != CLIENT_STATUS.LOGGED_IN)
            {
                HandleLogin(client, message);
                return;
            }

            Console.WriteLine("MESSAGE: " + message);

            if (message.Equals("kickmyass") || message.Equals("logout") || message.Equals("exit"))
            {
                s_server.SendMessageToClient(client, Environment.NewLine + Server.CURSOR);
                s_server.KickClient(client);
            }
            else if (message.Equals("clear"))
            {
                s_server.ClearClientScreen(client);
                s_server.SendMessageToClient(client, Server.CURSOR);
            }
            else
                s_server.SendMessageToClient(client, Environment.NewLine + Server.CURSOR);
        }

        private static void HandleLogin(Client client, string message)
        {
            if (client.CurrentStatus == CLIENT_STATUS.GUEST)
            {
                if (message.Equals("root"))
                {
                    s_server.SendMessageToClient(client, Environment.NewLine + "Password: ");
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
                    s_server.SendMessageToClient(client, $"Successfully authenticated.{Environment.NewLine}{Server.CURSOR}");
                    client.CurrentStatus = CLIENT_STATUS.LOGGED_IN;
                }
                else
                    s_server.KickClient(client);
            }
        }
    }
}