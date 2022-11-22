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
            Console.WriteLine($"Listening on {rxEndpoint.Address}:{rxEndpoint.Port}");
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
                Console.WriteLine("Valid message recieved.\n");
                Console.WriteLine($"Socket server {Id} received message: \"{message.Replace(Messages.EOM, "")}\"");

                switch (Messages.GetMessageType(message))
                {
                    case OperationType.Discover:
                        HandleDiscoverMessage(message);
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
            AddNewKnownNode(message);
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
    }

}
