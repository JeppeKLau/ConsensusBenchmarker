using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;
using ConsensusBenchmarker.Models.Events;
using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ConsensusBenchmarker.Communication
{
    public class CommunicationModule
    {
        private readonly int sharedPortNumber = 11_000;
        private readonly IPAddress? ipAddress;
        private readonly IPEndPoint? rxEndpoint;
        private readonly Socket? server;
        private readonly uint receivableByteSize = 4096;

        private readonly List<IPAddress> knownNodes = new();
        private readonly Stack<IEvent> eventStack;
        private readonly int nodeId;
        private bool ExecutionFlag;

        public CommunicationModule(ref Stack<IEvent> eventStack, int nodeId)
        {
            ipAddress = GetLocalIPAddress();
            rxEndpoint = new(ipAddress!, sharedPortNumber);
            server = new Socket(rxEndpoint!.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            knownNodes.Add(ipAddress!);
            this.eventStack = eventStack;
            this.nodeId = nodeId;
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

        public async Task RunCommunication(CancellationToken cancellationToken = default)
        {
            while (!DataCollectionReady()) ;

            var eventTask = HandleEventStack();
            var messageTask = WaitForMessage(cancellationToken);

            await Task.WhenAll(messageTask, eventTask);
        }

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

        private async Task HandleEventStack()
        {
            if (eventStack.Peek() is not CommunicationEvent nextEvent) return;

            switch (nextEvent.EventType)
            {
                case CommunicationEventType.End:
                    ExecutionFlag = false;
                    break;
                case CommunicationEventType.SendTransaction:
                    await BroadcastTransaction(nextEvent.Data as Transaction ?? throw new ArgumentException("Transaction missing from event", nameof(nextEvent.Data)));
                    break;
                case CommunicationEventType.SendBlock:
                    await BroadcastBlock(nextEvent.Data as Block ?? throw new ArgumentException("Block missing from event", nameof(nextEvent.Data)));
                    break;
                default:
                    throw new ArgumentException("Unknown event type", nameof(nextEvent.EventType));
            }
            eventStack.Pop();
        }

        private async Task BroadcastTransaction(Transaction transaction)
        {
            string messageToSend = Messages.CreateTRAMessage(transaction);
            BroadcastMessageAndDontWaitForAnswer(messageToSend);
        }

        private async Task BroadcastBlock(Block block)
        {
            string messageToSend = Messages.CreateBLOMessage(block);
            BroadcastMessageAndDontWaitForAnswer(messageToSend);
        }

        private async Task BroadcastMessageAndDontWaitForAnswer(string messageToSend)
        {
            foreach (var otherNode in knownNodes)
            {
                await SendMessageAndDontWaitForAnswer(otherNode, messageToSend);
            }
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
            var networkManagerEndpoint = new IPEndPoint(receiver, sharedPortNumber);
            byte[] encodedMessage = Encoding.UTF8.GetBytes(message);
            byte[] responseBuffer = new byte[receivableByteSize];

            var networkManager = new Socket(networkManagerEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await networkManager.ConnectAsync(networkManagerEndpoint, cancellationToken);
            _ = await networkManager.SendAsync(encodedMessage, SocketFlags.None, cancellationToken);
            int responseBytes = await networkManager.ReceiveAsync(responseBuffer, SocketFlags.None);
            networkManager.Shutdown(SocketShutdown.Both);
            networkManager.Close();
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
            var nodeEndpoint = new IPEndPoint(receiver, sharedPortNumber);
            byte[] encodedMessage = Encoding.UTF8.GetBytes(message);

            var nodeManager = new Socket(nodeEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await nodeManager.ConnectAsync(nodeEndpoint, cancellationToken);
            _ = await nodeManager.SendAsync(encodedMessage, SocketFlags.None, cancellationToken);
            nodeManager.Shutdown(SocketShutdown.Both);
        }

        #endregion

        #region HandleInputMessages

        private async Task WaitForMessage(CancellationToken cancellationToken = default)
        {
            server!.Bind(rxEndpoint!);
            server!.Listen(1000);
            Console.WriteLine($"Node listening on {rxEndpoint!.Address}:{rxEndpoint.Port}");

            while (ExecutionFlag)
            {
                var handler = await server!.AcceptAsync(cancellationToken);
                var rxBuffer = new byte[receivableByteSize];
                var bytesReceived = await handler.ReceiveAsync(rxBuffer, SocketFlags.None, cancellationToken);
                string message = Encoding.UTF8.GetString(rxBuffer, 0, bytesReceived);

                await HandleMessage(message, handler, cancellationToken);
            }
            //when total blocks has been reached, send all collected data to networkmanager ish?
        }

        private bool DataCollectionReady()
        {
            if (eventStack.Peek() is not DataCollectionEvent nextEvent) return false;
            if (nextEvent.EventType == DataCollectionEventType.CollectionReady)
            {
                return true;
            }
            return false;
        }

        private async Task HandleMessage(string message, Socket handler, CancellationToken cancellationToken = default)
        {
            if (Messages.DoesMessageContainOperationTag(message, OperationType.EOM))
            {
                Console.WriteLine($"Complete message recieved:\n{message}");
                string messageWithoutEOM = Messages.RemoveOperationTypeTag(message, OperationType.EOM);
                var operationType = Messages.GetOperationTypeEnum(messageWithoutEOM);

                switch (operationType)
                {
                    case OperationType.DEF:
                        throw new ArgumentOutOfRangeException($"Operation type {operationType} was not recognized.");
                    case OperationType.DIS:
                        SaveNewIPAddresses(Messages.RemoveOperationTypeTag(messageWithoutEOM, OperationType.DIS));
                        break;
                    case OperationType.TRA:
                        RecieveTransaction(Messages.RemoveOperationTypeTag(messageWithoutEOM, OperationType.TRA));
                        break;
                    case OperationType.BLK:
                        ReceiveBlock(Messages.RemoveOperationTypeTag(messageWithoutEOM, OperationType.BLK));
                        break;
                }
            }
        }

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
            if (JsonConvert.DeserializeObject<Transaction>(message) is not Transaction recievedTransaction)
            {
                throw new ArgumentException("Transaction could not be deserialized correctly", nameof(recievedTransaction));
            }
            eventStack.Push(new ConsensusEvent(recievedTransaction, ConsensusEventType.RecieveTransaction));
        }

        private void ReceiveBlock(string message)
        {
            if (JsonConvert.DeserializeObject<Block>(message) is not Block recievedBlock)
            {
                throw new ArgumentException("Block could not be deserialized correctly", nameof(recievedBlock));
            }
            eventStack.Push(new ConsensusEvent(recievedBlock, ConsensusEventType.RecieveBlock));
        }

        #endregion

    }
}
