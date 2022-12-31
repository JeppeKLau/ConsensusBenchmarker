using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;
using ConsensusBenchmarker.Models.DTOs;
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

        private readonly Dictionary<int, string> knownNodes = new();
        private readonly ConcurrentQueue<IEvent> eventQueue;
        private readonly int nodeId;
        private bool executionFlag;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly SemaphoreSlim knownNodesSemaphore = new(1, 1);

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

        private string? GetAddressByNodeId(int nodeId)
        {
            knownNodesSemaphore.Wait();
            var value = knownNodes.GetValueOrDefault(nodeId);
            knownNodesSemaphore.Release();
            return value;
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
                    eventQueue.Enqueue(new DataCollectionEvent(nodeId, DataCollectionEventType.IncTransaction, null));
                    await BroadcastTransaction(nextEvent.Data as Transaction ?? throw new ArgumentException("Transaction missing from event", nameof(nextEvent.Data)));
                    break;
                case CommunicationEventType.SendBlock:
                    await BroadcastBlock(nextEvent.Data as Block ?? throw new ArgumentException("Block missing from event", nameof(nextEvent.Data)));
                    break;
                case CommunicationEventType.RequestBlockChain:
                    await SendRequestBlockChain();
                    break;
                case CommunicationEventType.RecieveBlockChain:
                    {
                        var recipient = nextEvent.Recipient ?? throw new ArgumentException("string missing from event", nameof(nextEvent.Recipient));
                        await SendRecieveBlockChain(nextEvent.Data as List<Block> ?? throw new ArgumentException("Blockchain missing from event"), recipient);
                    }
                    break;
                case CommunicationEventType.RequestVote:
                    {
                        var voteRequest = nextEvent.Data as RaftVoteRequest ?? throw new ArgumentException("VoteRequest missing from event", nameof(nextEvent.Data));
                        var recipient = nextEvent.Recipient;
                        await SendMessageToRecipientOrBroadcast(Messages.CreateRQVMessage(voteRequest), recipient);
                    }
                    break;
                case CommunicationEventType.CastVote:
                    var voteRecipient = nextEvent.Recipient ?? throw new ArgumentException("Recipient missing from event", nameof(nextEvent.Recipient));
                    var voteResponse = nextEvent.Data as RaftVoteResponse ?? throw new ArgumentException("VoteResponse missing from event", nameof(nextEvent.Data));
                    await SendMessageAndDontWaitForAnswerIfRecipientExists(voteRecipient, Messages.CreateRCVMessage(voteResponse));
                    break;
                case CommunicationEventType.RequestHeartbeat:
                    {
                        var heartbeatRequest = nextEvent.Data as RaftHeartbeatRequest ?? throw new ArgumentException("HeartbeatRequest missing from event", nameof(nextEvent.Data));
                        var recipient = nextEvent.Recipient;
                        await SendMessageToRecipientOrBroadcast(Messages.CreateRQHMessage(heartbeatRequest), recipient);
                    }
                    break;
                case CommunicationEventType.ReceiveHeartbeat:
                    var heartbeatResponse = nextEvent.Data as RaftHeartbeatResponse ?? throw new ArgumentException("HeartbeatResponse missing from event", nameof(nextEvent.Data));
                    var heartbeatRecipient = nextEvent.Recipient ?? throw new ArgumentException("Recipient missing from event", nameof(nextEvent.Recipient));
                    await SendMessageAndDontWaitForAnswerIfRecipientExists(heartbeatRecipient, Messages.CreateRCHMessage(heartbeatResponse));
                    break;
                default:
                    throw new ArgumentException("Unknown event type", nameof(nextEvent.EventType));
            }
            eventQueue.TryDequeue(out _);
        }

        #region HandleOutputMessages

        /// <summary>
        /// Makes the node announce itself to the network manager, and receives a list of the network managers currently known nodes.
        /// </summary>
        /// <returns><see cref="Task"/></returns>
        public async Task AnnounceOwnIP()
        {
            var networkManagerIP = "192.168.100.100";
            string messageToSend = Messages.CreateDISMessage(ipAddress.ToString(), nodeId);
            string response = await SendMessageAndWaitForAnswer(networkManagerIP, messageToSend);

            if (Messages.DoesMessageContainOperationTag(response, OperationType.DIS))
            {
                response = Messages.RemoveOperationTypeTag(response, OperationType.DIS);
                response = Messages.RemoveOperationTypeTag(response, OperationType.EOM);

                if (string.IsNullOrEmpty(response)) return;

                var newNodes = JsonConvert.DeserializeObject<Dictionary<int, string>>(response) ?? throw new ArgumentNullException(nameof(response));

                foreach (var node in newNodes)
                {
                    AddNewNode(node);
                }
            }
            else
            {
                Console.WriteLine("No discover response from the Network Manager. Retrying");
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
            KeyValuePair<int, string>? lastNetworkNode = null;

            knownNodesSemaphore.Wait();
            if (knownNodes.Count > 0)
            {
                var random = new Random().Next(0, knownNodes.Count);
                lastNetworkNode = knownNodes.Skip(random).First();
            }
            knownNodesSemaphore.Release();

            if (lastNetworkNode == null)
            {
                Console.WriteLine($"I (node {nodeId}) want to request a blockchain, but I don't know any nodes.");
                eventQueue.Enqueue(new ConsensusEvent(new List<Block>(), ConsensusEventType.RecieveBlockchain, null));
            }
            else
            {
                Console.WriteLine($"I (node {nodeId}) requests recipient {lastNetworkNode}'s blockchain.");
                string messageToSend = Messages.CreateReqBCMessage(nodeId);
                await SendMessageAndDontWaitForAnswer(lastNetworkNode.Value.Value, messageToSend);
            }
        }

        private async Task SendRecieveBlockChain(List<Block> blocks, int recipient)
        {
            Console.WriteLine($"I (node {nodeId}) is sending my blockchain of {blocks.Count} length to {recipient}.");
            var messageToSend = Messages.CreateRecBCMessage(blocks);

            await SendMessageAndDontWaitForAnswerIfRecipientExists(recipient, messageToSend);
        }

        private async Task SendMessageToRecipientOrBroadcast(string message, int? nodeId)
        {
            if (nodeId == null)
            {
                await BroadcastMessageAndDontWaitForAnswer(message);
            }
            else
            {
                await SendMessageAndDontWaitForAnswerIfRecipientExists(nodeId.Value, message);
            }
        }

        private async Task BroadcastMessageAndDontWaitForAnswer(string messageToSend)
        {
            knownNodesSemaphore.Wait();
            foreach (var otherNode in knownNodes)
            {
                await SendMessageAndDontWaitForAnswer(otherNode.Value, messageToSend);
            }
            knownNodesSemaphore.Release();
        }

        private async Task SendMessageAndDontWaitForAnswerIfRecipientExists(int nodeId, string message)
        {
            var receiver = GetAddressByNodeId(nodeId);
            if (receiver is not null)
            {
                await SendMessageAndDontWaitForAnswer(receiver, message);
            }
        }

        private async Task<string> SendMessageAndWaitForAnswer(string receiver, string message, CancellationToken cancellationToken = default)
        {
            var receiverIP = IPAddress.Parse(receiver);
            eventQueue.Enqueue(new DataCollectionEvent(nodeId, DataCollectionEventType.OutMessage, DateTime.UtcNow));
            var networkManagerEndpoint = new IPEndPoint(receiverIP, sharedPortNumber);
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

        private async Task SendMessageAndDontWaitForAnswer(string receiver, string message, CancellationToken cancellationToken = default)
        {
            var receiverIP = IPAddress.Parse(receiver);
            eventQueue.Enqueue(new DataCollectionEvent(nodeId, DataCollectionEventType.OutMessage, DateTime.UtcNow));
            var nodeEndpoint = new IPEndPoint(receiverIP, sharedPortNumber);
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
                nodeManager.Shutdown(SocketShutdown.Both);
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
                    case OperationType.DIS:
                        SaveNewIPAddresses(Messages.RemoveOperationTypeTag(cleanMessageWithoutEOM, OperationType.DIS));
                        break;
                    case OperationType.TRA:
                        ReceiveTransaction(Messages.RemoveOperationTypeTag(cleanMessageWithoutEOM, OperationType.TRA));
                        break;
                    case OperationType.BLK:
                        ReceiveBlock(Messages.RemoveOperationTypeTag(cleanMessageWithoutEOM, OperationType.BLK));
                        break;
                    case OperationType.RQB:
                        RequestBlockChain(Messages.RemoveOperationTypeTag(cleanMessageWithoutEOM, OperationType.RQB));
                        break;
                    case OperationType.RCB:
                        ReceiveBlockChain(Messages.RemoveOperationTypeTag(cleanMessageWithoutEOM, OperationType.RCB));
                        break;
                    case OperationType.RQV:
                        RequestVote(Messages.RemoveOperationTypeTag(cleanMessageWithoutEOM, OperationType.RQV));
                        break;
                    case OperationType.RCV:
                        ReceiveVote(Messages.RemoveOperationTypeTag(cleanMessageWithoutEOM, OperationType.RCV));
                        break;
                    case OperationType.RQH:
                        RequestHeartBeat(Messages.RemoveOperationTypeTag(cleanMessageWithoutEOM, OperationType.RQH));
                        break;
                    case OperationType.RCH:
                        ReceiveHeartBeat(Messages.RemoveOperationTypeTag(cleanMessageWithoutEOM, OperationType.RCH));
                        break;
                    case OperationType.DEF:
                        Console.WriteLine($"Could not find any operation type tag on a msg: {cleanMessageWithoutEOM}");
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
            var newNode = JsonConvert.DeserializeObject<KeyValuePair<int, string>>(message);
            AddNewNode(newNode);
        }

        private void AddNewNode(KeyValuePair<int, string> newNode)
        {
            if (!knownNodes.ContainsKey(newNode.Key) && !newNode.Value.Equals(ipAddress))
            {
                knownNodesSemaphore.Wait();
                knownNodes.Add(newNode.Key, newNode.Value);
                knownNodesSemaphore.Release();
            }
        }

        private void ReceiveTransaction(string message)
        {
            if (JsonConvert.DeserializeObject<Transaction>(message) is not Transaction recievedTransaction)
            {
                throw new ArgumentException("Transaction could not be deserialized correctly", nameof(message));
            }
            eventQueue.Enqueue(new ConsensusEvent(recievedTransaction, ConsensusEventType.RecieveTransaction, null));
            eventQueue.Enqueue(new DataCollectionEvent(nodeId, DataCollectionEventType.IncTransaction, null));
        }

        private void ReceiveBlock(string message)
        {
            if (JsonConvert.DeserializeObject<Block>(message) is not Block recievedBlock)
            {
                throw new ArgumentException("Block could not be deserialized correctly", nameof(message));
            }
            eventQueue.Enqueue(new ConsensusEvent(recievedBlock, ConsensusEventType.RecieveBlock, null));
        }

        private void RequestBlockChain(string message)
        {
            var recipientId = int.Parse(message);
            Console.WriteLine($"Node {recipientId} requested my (node {nodeId})'s blockchain.");
            eventQueue.Enqueue(new ConsensusEvent(null, ConsensusEventType.RequestBlockchain, recipientId));
        }

        private void ReceiveBlockChain(string message)
        {
            if (message == string.Empty)
            {
                Console.WriteLine($"I (node {nodeId}) recieved a blockchain with 0 blocks.");
                eventQueue.Enqueue(new ConsensusEvent(new List<Block>(), ConsensusEventType.RecieveBlockchain, null));
            }
            else
            {
                if (JsonConvert.DeserializeObject<List<Block>>(message) is not List<Block> recievedBlocks)
                {
                    throw new ArgumentException("Blocks could not be deserialized correctly", nameof(message));
                }
                Console.WriteLine($"I (node {nodeId}) recieved a blockchain with {recievedBlocks.Count} blocks, latest block was created by: {recievedBlocks.Last().OwnerNodeID}");
                eventQueue.Enqueue(new ConsensusEvent(recievedBlocks, ConsensusEventType.RecieveBlockchain, null));
            }
        }

        private void RequestVote(string message)
        {
            if (JsonConvert.DeserializeObject<RaftVoteRequest>(message) is not RaftVoteRequest recievedVoteRequest)
            {
                throw new ArgumentException("VoteRequest could not be deserialized correctly", nameof(message));
            }
            eventQueue.Enqueue(new ConsensusEvent(recievedVoteRequest, ConsensusEventType.RequestVote, null));
        }

        private void ReceiveVote(string message)
        {
            if (JsonConvert.DeserializeObject<RaftVoteResponse>(message) is not RaftVoteResponse recievedVoteResponse)
            {
                throw new ArgumentException("VoteRequest could not be deserialized correctly", nameof(message));
            }
            eventQueue.Enqueue(new ConsensusEvent(recievedVoteResponse, ConsensusEventType.ReceiveVote, null));
        }

        private void RequestHeartBeat(string message)
        {
            if (JsonConvert.DeserializeObject<RaftHeartbeatRequest>(message) is not RaftHeartbeatRequest recievedHeartbeat)
            {
                throw new ArgumentException("Heartbeat could not be deserialized correctly", nameof(message));
            }
            eventQueue.Enqueue(new ConsensusEvent(recievedHeartbeat, ConsensusEventType.RequestHeartbeat, null));
        }

        private void ReceiveHeartBeat(string message)
        {
            if (JsonConvert.DeserializeObject<RaftHeartbeatResponse>(message) is not RaftHeartbeatResponse recievedHeartbeatResponse)
            {
                throw new ArgumentException("HeartbeatResponse could not be deserialized correctly", nameof(message));
            }
            eventQueue.Enqueue(new ConsensusEvent(recievedHeartbeatResponse, ConsensusEventType.ReceiveHeartbeat, null));
        }

        #endregion

    }
}
