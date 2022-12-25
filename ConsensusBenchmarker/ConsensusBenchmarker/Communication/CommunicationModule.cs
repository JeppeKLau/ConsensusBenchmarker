using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;
using ConsensusBenchmarker.Models.Events;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ConsensusBenchmarker.Communication
{
    public class CommunicationModule
    {
        private readonly int sharedPortNumber = 11_000;
        private readonly IPAddress ipAddress;
        private readonly IPEndPoint rxEndpoint;
        private readonly uint receivableByteSize = 50 * 1024;

        private readonly List<IPAddress> knownNodes = new();
        private readonly ConcurrentQueue<IEvent> eventQueue;
        private readonly int nodeId;
        private bool executionFlag;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly SemaphoreSlim knownNodesSemaphore = new(1, 1);
        private readonly JsonSerializerSettings jsonSettings = new() { TypeNameHandling = TypeNameHandling.All };

        public CommunicationModule(ref ConcurrentQueue<IEvent> eventQueue, int nodeId)
        {
            ipAddress = GetLocalIPAddress();
            rxEndpoint = new(ipAddress, sharedPortNumber);
            this.eventQueue = eventQueue;
            this.nodeId = nodeId;
            executionFlag = true;
            cancellationTokenSource = new CancellationTokenSource();
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

        public void SpawnThreads(Dictionary<string, Thread> moduleThreads)
        {
            while (!DataCollectionReady()) ;

            moduleThreads.Add("Communication_WaitForMessage", new Thread(() =>
            {
                WaitForMessage(cancellationTokenSource.Token).GetAwaiter().GetResult();
            }));

            moduleThreads.Add("Communication_HandleEventLoop", new Thread(() =>
            {
                while (executionFlag || !eventQueue.IsEmpty)
                {
                    HandleEventQueue().GetAwaiter().GetResult();
                    Thread.Sleep(1);
                }
                cancellationTokenSource.Cancel();
            }));
        }

        private bool DataCollectionReady()
        {
            if (!eventQueue.TryPeek(out var @event)) return false;
            if (@event is not DataCollectionEvent nextEvent) return false;

            if (nextEvent.EventType == DataCollectionEventType.CollectionReady)
            {
                eventQueue.TryDequeue(out _);
                return true;
            }
            return false;
        }

        private async Task HandleEventQueue()
        {
            if (!eventQueue.TryPeek(out var @event)) return;
            if (@event is not CommunicationEvent nextEvent) return;

            switch (nextEvent.EventType)
            {
                case CommunicationEventType.End:
                    executionFlag = false;
                    Console.WriteLine("Communication was signalled to end.");
                    break;
                case CommunicationEventType.SendTransaction:
                    eventQueue.Enqueue(new DataCollectionEvent(nodeId, DataCollectionEventType.IncTransaction, nextEvent.Data));
                    await BroadcastTransaction(nextEvent.Data as Transaction ?? throw new ArgumentException("Transaction missing from event", nameof(nextEvent.Data)));
                    break;
                case CommunicationEventType.SendBlock:
                    await BroadcastBlock(nextEvent.Data as Models.Blocks.Block ?? throw new ArgumentException("Block missing from event", nameof(nextEvent.Data)));
                    break;
                case CommunicationEventType.RequestBlockChain:
                    await SendRequestBlockChain();
                    break;
                case CommunicationEventType.RecieveBlockChain:
                    await SendRecieveBlockChain(nextEvent.Data as List<Block> ?? throw new ArgumentException("Blockchain missing from event"), nextEvent.Recipient!);
                    break;
                default:
                    throw new ArgumentException("Unknown event type", nameof(nextEvent.EventType));
            }
            eventQueue.TryDequeue(out _);
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

        private async Task BroadcastTransaction(Transaction transaction)
        {
            string messageToSend = Messages.CreateTRAMessage(transaction);
            await BroadcastMessageAndDontWaitForAnswer(messageToSend);
        }

        private async Task BroadcastBlock(Models.Blocks.Block block)
        {
            string messageToSend = Messages.CreateBLOMessage(block);
            await BroadcastMessageAndDontWaitForAnswer(messageToSend);
        }

        private async Task SendRequestBlockChain()
        {
            IPAddress? firstNetworkNode = null;

            knownNodesSemaphore.Wait();
            if (knownNodes.Count > 0)
            {
                firstNetworkNode = knownNodes.First();
            }
            knownNodesSemaphore.Release();

            if (firstNetworkNode == null)
            {
                Console.WriteLine($"I (node {nodeId}) want to request a blockchain, but I don't know any nodes.");
                eventQueue.Enqueue(new ConsensusEvent(new List<Block>(), ConsensusEventType.RecieveBlockchain, null)); // ConsensusModule needs response in order to begin its consensus.
            }
            else
            {
                Console.WriteLine($"I (node {nodeId}) requests recipient {firstNetworkNode}'s blockchain.");
                string messageToSend = Messages.CreateReqBCMessage(ipAddress!);
                await SendMessageAndDontWaitForAnswer(firstNetworkNode, messageToSend);
            }
        }

        private async Task SendRecieveBlockChain(List<Block> blocks, IPAddress recipient)
        {
            //Console.WriteLine($"I (node {nodeId}) is sending my blockchain of {blocks.Count} length to {recipient}.");
            var messageToSend = Messages.CreateRecBCMessage(blocks);

            //Console.WriteLine($"Sending: {messageToSend}");
            await SendMessageAndDontWaitForAnswer(recipient, messageToSend);
        }

        private async Task BroadcastMessageAndDontWaitForAnswer(string messageToSend)
        {
            knownNodesSemaphore.Wait();
            foreach (var otherNode in knownNodes)
            {
                await SendMessageAndDontWaitForAnswer(otherNode, messageToSend);
            }
            knownNodesSemaphore.Release();
        }

        /// <summary>
        /// Send a message to another IP address and waits for the answer.
        /// </summary>
        /// <param name="receiver"></param>
        /// <param name="message"></param>
        /// <param name="cancellationToken"></param>
        /// <returns><see cref="string"/></returns>
        private async Task<string> SendMessageAndWaitForAnswer(IPAddress receiver, string message, CancellationToken cancellationToken = default)
        {
            var networkManagerEndpoint = new IPEndPoint(receiver, sharedPortNumber);
            byte[] encodedMessage = Encoding.UTF8.GetBytes(message);
            byte[] responseBuffer = new byte[receivableByteSize];
            var networkManager = new Socket(networkManagerEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await networkManager.ConnectAsync(networkManagerEndpoint, cancellationToken);
                _ = await networkManager.SendAsync(encodedMessage, SocketFlags.None, cancellationToken);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Connecting to address: {receiver} failed with error code: {ex.ErrorCode}\n\t{ex.Message}");
            }
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
        /// <returns><see cref="Task"/></returns>
        private async Task SendMessageAndDontWaitForAnswer(IPAddress receiver, string message, CancellationToken cancellationToken = default)
        {
            var nodeEndpoint = new IPEndPoint(receiver, sharedPortNumber);
            byte[] encodedMessage = Encoding.UTF8.GetBytes(message);
            var nodeManager = new Socket(nodeEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await nodeManager.ConnectAsync(nodeEndpoint, cancellationToken);
                _ = await nodeManager.SendAsync(encodedMessage, SocketFlags.None, cancellationToken);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Connecting to address: {receiver} failed with error code: {ex.ErrorCode}\n\t{ex.Message}");
            }
            if (nodeManager.Connected)
            {
                nodeManager.Shutdown(SocketShutdown.Both); // this threw an error for me
            }
        }

        #endregion

        #region HandleInputMessages

        private async Task WaitForMessage(CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"Node listening on {rxEndpoint.Address}:{rxEndpoint.Port}");

            using Socket server = new(rxEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            server.Bind(rxEndpoint);
            server.Listen(1000);

            while (executionFlag)
            {
                try
                {
                    var handler = await server.AcceptAsync(cancellationToken);
                    var rxBuffer = new byte[receivableByteSize];
                    var bytesReceived = await handler.ReceiveAsync(rxBuffer, SocketFlags.None, cancellationToken);
                    string message = Encoding.UTF8.GetString(rxBuffer, 0, bytesReceived);

                    HandleMessage(message);
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Terminating messaging");
                    continue;
                }
            }
        }

        private void HandleMessage(string message)
        {
            if (Messages.DoesMessageContainOperationTag(message, OperationType.EOM))
            {
                string cleanMessageWithoutEOM = Messages.RemoveOperationTypeTag(Messages.TrimUntillTag(message), OperationType.EOM);
                var operationType = Messages.GetOperationTypeEnum(cleanMessageWithoutEOM);

                eventQueue.Enqueue(new DataCollectionEvent(nodeId, DataCollectionEventType.IncMessage, cleanMessageWithoutEOM));

                switch (operationType)
                {
                    case OperationType.DEF:
                        Console.WriteLine($"Could not find any operation type tag on a msg: {cleanMessageWithoutEOM}");
                        break;
                    case OperationType.DIS:
                        SaveNewIPAddresses(Messages.RemoveOperationTypeTag(cleanMessageWithoutEOM, OperationType.DIS));
                        break;
                    case OperationType.TRA:
                        RecieveTransaction(Messages.RemoveOperationTypeTag(cleanMessageWithoutEOM, OperationType.TRA));
                        break;
                    case OperationType.BLK:
                        ReceiveBlock(Messages.RemoveOperationTypeTag(cleanMessageWithoutEOM, OperationType.BLK));
                        break;
                    case OperationType.QBC:
                        RequestBlockChain(Messages.RemoveOperationTypeTag(cleanMessageWithoutEOM, OperationType.QBC));
                        break;
                    case OperationType.RBC:
                        Console.WriteLine("Comms: Recieved a blockchain type operation msg.");
                        RecieveBlockChain(Messages.RemoveOperationTypeTag(cleanMessageWithoutEOM, OperationType.RBC));
                        break;
                }
            }
            else
            {
                Console.WriteLine("Comms: Could not find EOM tag.");
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
                if (!knownNodes.Contains(newIP) && !newIP.Equals(ipAddress))
                {
                    knownNodesSemaphore.Wait();
                    knownNodes.Add(newIP);
                    knownNodesSemaphore.Release();
                }
            }
        }

        private void RecieveTransaction(string message)
        {
            if (JsonConvert.DeserializeObject<Transaction>(message) is not Transaction recievedTransaction)
            {
                throw new ArgumentException("Transaction could not be deserialized correctly", nameof(message));
            }
            eventQueue.Enqueue(new ConsensusEvent(recievedTransaction, ConsensusEventType.RecieveTransaction, null));
        }

        private void ReceiveBlock(string message)
        {
            if (JsonConvert.DeserializeObject<Block>(message, jsonSettings) is not Block recievedBlock)
            {
                throw new ArgumentException("Block could not be deserialized correctly", nameof(message));
            }
            eventQueue.Enqueue(new ConsensusEvent(recievedBlock, ConsensusEventType.RecieveBlock, null));
        }

        private void RequestBlockChain(string message)
        {
            IPAddress recipientNode = Messages.ParseIpAddress(message);
            Console.WriteLine($"Node {recipientNode} requested my (node {nodeId}) blockchain.");
            eventQueue.Enqueue(new ConsensusEvent(null, ConsensusEventType.RequestBlockchain, recipientNode));
        }

        private void RecieveBlockChain(string message)
        {
            if (message == string.Empty)
            {
                eventQueue.Enqueue(new ConsensusEvent(new List<Block>(), ConsensusEventType.RecieveBlockchain, null));
            }
            else
            {
                if (JsonConvert.DeserializeObject<List<Block>>(message, jsonSettings) is not List<Block> recievedBlocks)
                {
                    throw new ArgumentException("Blocks could not be deserialized correctly", nameof(message));
                }
                Console.WriteLine($"I (node {nodeId}) recieved a blockchain with {recievedBlocks.Count} blocks, latest block was created by: {recievedBlocks.Last().OwnerNodeID}");
                eventQueue.Enqueue(new ConsensusEvent(recievedBlocks, ConsensusEventType.RecieveBlockchain, null));
            }
        }

        #endregion

    }
}
