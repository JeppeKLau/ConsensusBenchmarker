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
        private readonly string consensusType;

        private DataCollectionModule dataCollectionModule = new DataCollectionModule();
        private ConsensusModule consensusModule = new ConsensusModule();

        private readonly int sharedPortNumber = 11_000;
        private readonly IPAddress? ipAddress;
        private readonly IPEndPoint? rxEndpoint;
        private readonly Socket? server;
        private readonly uint receivableByteSize = 4096;

        private List<IPAddress> knownNodes = new();

        public CommunicationModule(string consensus)
        {
            consensusType = consensus;
            ipAddress = GetLocalIPAddress();
            rxEndpoint = new(ipAddress!, sharedPortNumber);
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

        #endregion

        #region HandleOutputMessages

        public async Task AnnounceOwnIP()
        {
            var networkManagerIP = new IPAddress(new byte[] { 192, 168, 100, 100 });
            string messageToSend = Messages.CreateDISMessage(ipAddress!);
            string response = await SendMessageAndWaitForAnswer(networkManagerIP, messageToSend);

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

        public async Task BroadcastTransaction(Transaction transaction)
        {
            string messageToSend = Messages.CreateTRAMessage(transaction);

            foreach (IPAddress otherNode in knownNodes)
            {
                await SendMessageAndDontWaitForAnswer(otherNode, messageToSend);
            }
        }

        public async Task BroadcastBlock()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Send a message to another IP address and waits for the answer.
        /// </summary>
        /// <param name="receiver"></param>
        /// <param name="message"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<string> SendMessageAndWaitForAnswer(IPAddress receiver, string message, CancellationToken cancellationToken = default)
        {
            IPEndPoint networkManagerEndpoint = new IPEndPoint(receiver, sharedPortNumber);
            byte[] encodedMessage = Encoding.UTF8.GetBytes(message);
            byte[] responseBuffer = new byte[receivableByteSize];

            Socket networkManager = new Socket(networkManagerEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await networkManager.ConnectAsync(networkManagerEndpoint, cancellationToken);
            _ = await networkManager.SendAsync(encodedMessage, SocketFlags.None, cancellationToken);
            int responseBytes = await networkManager.ReceiveAsync(responseBuffer, SocketFlags.None);
            networkManager.Shutdown(SocketShutdown.Both);
            //networkManager.Close();
            return Encoding.UTF8.GetString(responseBuffer, 0, responseBytes);
        }

        /// <summary>
        /// Send a message to another IP address without waiting for the answer.
        /// </summary>
        /// <param name="receiver"></param>
        /// <param name="message"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task SendMessageAndDontWaitForAnswer(IPAddress receiver, string message, CancellationToken cancellationToken = default)
        {
            IPEndPoint nodeEndpoint = new IPEndPoint(receiver, sharedPortNumber);
            byte[] encodedMessage = Encoding.UTF8.GetBytes(message);

            using (Socket nodeManager = new Socket(nodeEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                await nodeManager.ConnectAsync(nodeEndpoint, cancellationToken);
                _ = await nodeManager.SendAsync(encodedMessage, SocketFlags.None, cancellationToken);
                nodeManager.Shutdown(SocketShutdown.Both);
                //nodeManager.Close();
            }
        }

        #endregion
    }

}
