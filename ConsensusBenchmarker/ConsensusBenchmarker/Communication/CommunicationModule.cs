﻿using ConsensusBenchmarker.Models;
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
        private readonly IPAddress? ipAddress;
        private readonly IPEndPoint? rxEndpoint;
        private readonly Socket? server;
        private readonly uint receivableByteSize = 4096;

        private readonly List<IPAddress> knownNodes = new();
        private readonly ConcurrentQueue<IEvent> eventQueue;
        private readonly int nodeId;
        private bool ExecutionFlag;
        private readonly Mutex knownNodesMutex = new();

        public CommunicationModule(ref ConcurrentQueue<IEvent> eventQueue, int nodeId)
        {
            ipAddress = GetLocalIPAddress();
            rxEndpoint = new(ipAddress!, sharedPortNumber);
            server = new Socket(rxEndpoint!.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.eventQueue = eventQueue;
            this.nodeId = nodeId;
            ExecutionFlag = true;
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

        public List<Thread> SpawnThreads()
        {
            while (!DataCollectionReady()) ;

            var eventThread = new Thread(() =>
            {
                while (ExecutionFlag)
                {
                    HandleEventQueue().GetAwaiter().GetResult();
                }
            });

            var messageThread = new Thread(() =>
            {
                while (ExecutionFlag)
                {
                    WaitForMessage().GetAwaiter().GetResult();
                }
            });

            Console.WriteLine("Communication ready.");
            return new List<Thread>() { eventThread, messageThread };
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

        #region HandleOutputMessages

        public async Task AnnounceOwnIP()
        {
            var networkManagerIP = new IPAddress(new byte[] { 192, 168, 100, 100 }); // 192, 168, 100, 100 
            string messageToSend = Messages.CreateDISMessage(ipAddress!);
            Console.WriteLine("Announcing self to network manager");
            string response = await SendMessageAndWaitForAnswer(networkManagerIP, messageToSend);

            if (Messages.DoesMessageContainOperationTag(response, OperationType.ACK))
            {
                response = Messages.RemoveOperationTypeTag(response, OperationType.ACK);
                response = Messages.RemoveOperationTypeTag(response, OperationType.EOM);

                SaveNewIPAddresses(response);
                Console.WriteLine("Announced itself to the Network Manager, and recieved a list of known nodes.");
            }
            else
            {
                Console.WriteLine("No ACK from Network Manager. Retrying");
                await AnnounceOwnIP();
            }
        }

        private async Task HandleEventQueue()
        {
            if (!eventQueue.TryPeek(out var @event)) return;
            if (@event is not CommunicationEvent nextEvent) return;

            Console.WriteLine($"Handling Communication event with type: {nextEvent.EventType}");

            switch (nextEvent.EventType)
            {
                case CommunicationEventType.End:
                    ExecutionFlag = false;
                    break;
                case CommunicationEventType.SendTransaction:
                    eventQueue.Enqueue(new DataCollectionEvent(nodeId, DataCollectionEventType.IncTransaction, nextEvent.Data));
                    await BroadcastTransaction(nextEvent.Data as Transaction ?? throw new ArgumentException("Transaction missing from event", nameof(nextEvent.Data)));
                    break;
                case CommunicationEventType.SendBlock:
                    await BroadcastBlock(nextEvent.Data as Models.Blocks.Block ?? throw new ArgumentException("Block missing from event", nameof(nextEvent.Data)));
                    break;
                default:
                    throw new ArgumentException("Unknown event type", nameof(nextEvent.EventType));
            }
            eventQueue.TryDequeue(out _);
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

        private async Task BroadcastMessageAndDontWaitForAnswer(string messageToSend)
        {
            var acquired = knownNodesMutex.WaitOne();
            foreach (var otherNode in knownNodes)
            {
                await SendMessageAndDontWaitForAnswer(otherNode, messageToSend);
            }
            if (acquired) knownNodesMutex.ReleaseMutex();
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
            Console.WriteLine($"Sending message: {message} to {receiver}\n");
            var networkManagerEndpoint = new IPEndPoint(receiver, sharedPortNumber);
            byte[] encodedMessage = Encoding.UTF8.GetBytes(message);
            byte[] responseBuffer = new byte[receivableByteSize];

            var networkManager = new Socket(networkManagerEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await networkManager.ConnectAsync(networkManagerEndpoint, cancellationToken);
            _ = await networkManager.SendAsync(encodedMessage, SocketFlags.None, cancellationToken);
            int responseBytes = await networkManager.ReceiveAsync(responseBuffer, SocketFlags.None);
            Console.WriteLine($"Received answer: {Encoding.UTF8.GetString(responseBuffer, 0, responseBytes)}");
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
            Console.WriteLine($"Sending message: {message} to {receiver}\n\n");
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

                HandleMessage(message);
            }
        }

        private void HandleMessage(string message)
        {
            if (Messages.DoesMessageContainOperationTag(message, OperationType.EOM))
            {
                string cleanMessageWithoutEOM = Messages.RemoveOperationTypeTag(Messages.TrimUntillTag(message), OperationType.EOM);
                Console.WriteLine($"Message recieved.");

                eventQueue.Enqueue(new DataCollectionEvent(nodeId, DataCollectionEventType.IncMessage, message));
                var operationType = Messages.GetOperationTypeEnum(cleanMessageWithoutEOM);

                switch (operationType)
                {
                    case OperationType.DEF:
                        break;
                    case OperationType.DIS:
                        Console.WriteLine("Recieved discover message from the network manager: " + cleanMessageWithoutEOM); // TEMP
                        SaveNewIPAddresses(Messages.RemoveOperationTypeTag(cleanMessageWithoutEOM, OperationType.DIS));
                        break;
                    case OperationType.TRA:
                        RecieveTransaction(Messages.RemoveOperationTypeTag(cleanMessageWithoutEOM, OperationType.TRA));
                        break;
                    case OperationType.BLK:
                        ReceiveBlock(Messages.RemoveOperationTypeTag(cleanMessageWithoutEOM, OperationType.BLK));
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
            Console.WriteLine("I (" + nodeId + ") know this many other nodes: " + knownNodes.Count);
        }

        private void AddNewNode(string DiscoverMessage)
        {
            if (DiscoverMessage.Contains('.'))
            {
                IPAddress newIP = Messages.ParseIpAddress(DiscoverMessage);
                if (!knownNodes.Contains(newIP) && !newIP.Equals(ipAddress))
                {
                    if (knownNodesMutex.WaitOne())
                    {
                        knownNodes.Add(newIP);
                        knownNodesMutex.ReleaseMutex();
                    }
                }
            }
        }

        private void RecieveTransaction(string message)
        {
            if (JsonConvert.DeserializeObject<Transaction>(message) is not Transaction recievedTransaction)
            {
                throw new ArgumentException("Transaction could not be deserialized correctly", nameof(message));
            }
            Console.WriteLine($"Recieved transaction message: " + message);
            eventQueue.Enqueue(new ConsensusEvent(recievedTransaction, ConsensusEventType.RecieveTransaction));
        }

        private void ReceiveBlock(string message)
        {
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
            if (JsonConvert.DeserializeObject<Models.Blocks.Block>(message, settings) is not Models.Blocks.Block recievedBlock)
            {
                throw new ArgumentException("Block could not be deserialized correctly", nameof(message));
            }
            Console.WriteLine($"Recieved block message: " + message);
            eventQueue.Enqueue(new ConsensusEvent(recievedBlock, ConsensusEventType.RecieveBlock));
        }

        #endregion

    }
}
