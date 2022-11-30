using ConsensusBenchmarker.Consensus;
using ConsensusBenchmarker.Data_Collection;
using ConsensusBenchmarker.Models;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ConsensusBenchmarker.Communication
{
    public class CommunicationModule
    {
        private DataCollectionModule dataCollectionModule = new DataCollectionModule();
        private ConsensusModule consensusModule = new ConsensusModule();

        private readonly IPAddress? ipAddress;
        private readonly IPEndPoint? rxEndpoint;
        private readonly Socket? server;
        private readonly uint receivableByteSize = 4096;

        private List<IPAddress> knownNodes = new();

        public CommunicationModule()
        {
            ipAddress = GetLocalIPAddress();
            rxEndpoint = new(ipAddress!, 11_000);
            server = new Socket(rxEndpoint!.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            knownNodes.Add(ipAddress!);
        }

        private IPAddress GetLocalIPAddress()
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

        public async Task WaitForMessage(CancellationToken cancellationToken = default)
        {
            server!.Bind(rxEndpoint!);
            server!.Listen(1000);

            while (true)
            {
                var handler = await server!.AcceptAsync(cancellationToken);
                var rxBuffer = new byte[receivableByteSize];
                var bytesReceived = await handler.ReceiveAsync(rxBuffer, SocketFlags.None, cancellationToken);
                string message = Encoding.UTF8.GetString(rxBuffer, 0, bytesReceived);

                await HandleMessage(message, handler, cancellationToken);
            }
        }

        #region HandleInputMessages

        private async Task HandleMessage(string message, Socket handler, CancellationToken cancellationToken = default)
        {
            if (Messages.DoesMessageContainOperationTag(message, OperationType.EOM))
            {
                Console.WriteLine("Valid message recieved.");
                string messageWithoutEOM = Messages.RemoveOperationTypeTag(message, OperationType.EOM);

                switch (Messages.GetOperationTypeEnum(messageWithoutEOM))
                {
                    case OperationType.DIS:
                        SaveNewIPAddresses(Messages.RemoveOperationTypeTag(messageWithoutEOM, OperationType.DIS));
                        break;
                    case OperationType.TRA:
                        RecieveTransaction(Messages.RemoveOperationTypeTag(messageWithoutEOM, OperationType.TRA));
                        break;
                    case OperationType.DEF:
                        throw new ArgumentOutOfRangeException("Operation type was not recognized.");

                }
            }
        }

        /// <summary>
        /// Finds a list of nodes from the message input which are then added to this node's list of known nodes.
        /// </summary>
        /// <param name="message"></param>
        private void SaveNewIPAddresses(string message)
        {
            var ipAddresses = message.Split(',');
            foreach (var ipAddress in ipAddresses)
            {
                AddNewNode(ipAddress);
            }
        }

        private void AddNewNode(string DiscoverMessage)
        {
            if (DiscoverMessage.Contains('.'))
            {
                IPAddress newIP = Messages.ParseIpAddress(DiscoverMessage);
                if (!knownNodes.Contains(newIP))
                {
                    knownNodes.Add(newIP);
                }
            }
        }

        private void RecieveTransaction(string message)
        {
            Transaction recievedTransaction = new Transaction(message);

            // send to consensus module

        }

        private Task ReceiveBlock()
        {
            throw new NotImplementedException();
        }

        private Task SendBlock()
        {
            throw new NotImplementedException();
        }

        #endregion

        public async Task AnnounceOwnIP()
        {
            var networkManagerIP = new IPAddress(new byte[] { 192, 168, 100, 100 });
            string messageToSend = Messages.CreateDISMessage(ipAddress!);
            string response = await SendMessage(networkManagerIP, 11_000, messageToSend);

            if (Messages.DoesMessageContainOperationTag(response, OperationType.ACK))
            {
                response = Messages.RemoveOperationTypeTag(response, OperationType.ACK);
                response = Messages.RemoveOperationTypeTag(response, OperationType.EOM);

                SaveNewIPAddresses(response);
            }
            else
            {
                Console.WriteLine("No ACK from Network Manager. Retrying");
                await AnnounceOwnIP();
            }
        }

        private async Task<string> SendMessage(IPAddress receiver, int portNumber, string message, CancellationToken cancellationToken = default)
        {
            IPEndPoint networkManagerEndpoint = new IPEndPoint(receiver, portNumber);
            Socket networkManager = new Socket(networkManagerEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            byte[] encodedMessage = Encoding.UTF8.GetBytes(message);

            while (!networkManager.Connected)
            {
                await networkManager.ConnectAsync(networkManagerEndpoint);
            }

            _ = await networkManager.SendAsync(encodedMessage, SocketFlags.None, cancellationToken);
            byte[] responseBuffer = new byte[receivableByteSize];
            int responseBytes = await networkManager.ReceiveAsync(responseBuffer, SocketFlags.None);
            networkManager.Shutdown(SocketShutdown.Both);
            return Encoding.UTF8.GetString(responseBuffer, 0, responseBytes);
        }
    }

}
