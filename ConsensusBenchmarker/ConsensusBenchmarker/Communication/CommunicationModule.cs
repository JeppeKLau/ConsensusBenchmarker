using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ConsensusBenchmarker.Communication
{
    public class CommunicationModule : ICommunicationModule
    {
        private readonly IPAddress ipAddress;
        private readonly IPEndPoint rxEndpoint;
        private readonly Socket server;
        private readonly int Id;
        private readonly uint receivableByteSize = 4096;

        private List<IPAddress> knownNodes = new List<IPAddress>();

        public CommunicationModule(int id)
        {
            Id = id;
            ipAddress = IPAddress.Loopback;
            rxEndpoint = new(ipAddress, 11_000 + Id);
            server = new Socket(rxEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            knownNodes.Add(ipAddress);
        }

        private static IPAddress GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip;
                }
            }
            throw new ApplicationException("Node has no ip address setup. Try restarting client");
        }

        public async Task WaitInstruction(CancellationToken cancellationToken = default)
        {
            server.Bind(rxEndpoint);
            server.Listen(1000);

            while (true)
            {
                Socket handler = await server.AcceptAsync(cancellationToken);
                var rxBuffer = new byte[receivableByteSize];
                var bytesReceived = await handler.ReceiveAsync(rxBuffer, SocketFlags.None, cancellationToken);
                string message = Encoding.UTF8.GetString(rxBuffer, 0, bytesReceived);

                await HandleMessage(message, handler, cancellationToken);

                var echoBytes = Encoding.UTF8.GetBytes(Messages.ACK);
                await handler.SendAsync(echoBytes, SocketFlags.None, cancellationToken);
                Console.WriteLine($"Socket server sent back acknowledgement: \"{Messages.ACK}\"\n\n");
            }
        }

        private async Task HandleMessage(string message, Socket handler, CancellationToken cancellationToken = default)
        {
            if (Messages.IsMessageValid(message))
            {
                Console.WriteLine("Valid message recieved.");
                Console.WriteLine($"Socket server {Id} received message: \"{message.Replace(Messages.EOM, "")}\"");

                switch (Messages.GetMessageType(message))
                {
                    case OperationType.Discover:
                        HandleDiscoverMessage(message.Remove(0, Messages.Discover.Length));
                        break;
                    case OperationType.Default:
                        break;
                }
            }
        }

        /// <summary>
        /// Another node has discovered this node and wants this nodes known nodes.
        /// </summary>
        /// <param name="message"></param>
        private void HandleDiscoverMessage(string message)
        {
            // Add new node to known node
            AddNewKnownNode(message.Remove(message.IndexOf(Messages.EOM), Messages.EOM.Length));

            // Tell them your known nodes
            //handler.SendAsync();
        }

        public void AddNewKnownNode(string DiscoverMessage)
        {
            IPAddress newIP = Messages.ParseIpAddress(DiscoverMessage);

            if (!knownNodes.Contains(newIP))
            {
                knownNodes.Add(newIP);
            }
        }

        public Task ReceiveBlock()
        {
            throw new NotImplementedException();
        }

        public Task SendBlock()
        {
            throw new NotImplementedException();
        }

        public async Task AnnounceOwnIP(CancellationToken cancellationToken = default)
        {
            var networkManagerIP = new IPAddress(new byte[] { 127, 0, 0, 1 });
            var networkManagerEndpoint = new IPEndPoint(networkManagerIP, 11_000);
            var networkManager = new Socket(networkManagerEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            var ownIPAddressMessage = Encoding.UTF8.GetBytes($"{Messages.Discover}IP:{ipAddress}{Messages.EOM}");

            await networkManager.ConnectAsync(networkManagerEndpoint);

            _ = await networkManager.SendAsync(ownIPAddressMessage, SocketFlags.None, cancellationToken);

            var responseBuffer = new byte[4096];
            var responseBytes = await networkManager.ReceiveAsync(responseBuffer, SocketFlags.None);
            var response = Encoding.UTF8.GetString(responseBuffer, 0, responseBytes);

            if (response.Contains(Messages.ACK))
            {
                response = response.Remove(0, Messages.ACK.Length);
                response = response.Remove(response.IndexOf(Messages.EOM), Messages.EOM.Length);
                var ipAddresses = response.Split(",");
                foreach (var address in ipAddresses)
                {
                    AddNewKnownNode(address);
                }
            }
            else
            {
                Console.WriteLine("No ACK from Network Manager. Retrying");
                await AnnounceOwnIP(cancellationToken);
            }

            networkManager.Shutdown(SocketShutdown.Both);
        }
    }

}
