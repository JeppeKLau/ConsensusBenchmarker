using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;
using ConsensusBenchmarker.Models.Events;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Reflection;

namespace ConsensusBenchmarker.Consensus
{
    public class ConsensusModule
    {
        private readonly string consensusType;
        private readonly ConsensusDriver consensusMechanism;
        private readonly ConcurrentQueue<IEvent> eventQueue;
        private readonly int NodeID;

        public ConsensusModule(string consensusType, int maxBlocksToCreate, int nodeID, ref ConcurrentQueue<IEvent> eventQueue)
        {
            this.consensusType = consensusType;
            consensusMechanism = InstantiateCorrespondingConsensusClass(nodeID, maxBlocksToCreate);
            this.eventQueue = eventQueue;
            NodeID = nodeID;
        }

        public void SpawnThreads(Dictionary<string, Thread> moduleThreads)
        {
            eventQueue.Enqueue(new ConsensusEvent(null, ConsensusEventType.CreateTransaction, null));
            moduleThreads.Add("Consensus_HandleEventLoop", new Thread(() =>
            {
                while (consensusMechanism.ExecutionFlag || eventQueue.Count > 0)
                {
                    HandleEventQueue();
                    Thread.Sleep(1);
                }
                NotifyModulesOfTestEnd();
            }));
            if (consensusType == "PoW") // Could probably be prettier
            {
                moduleThreads.Add("Consensus_PoWMining", new Thread(() =>
                {
                    while (consensusMechanism.ExecutionFlag)
                    {
                        HandleMiningOperation();
                    }
                }));
            }
        }

        private void HandleMiningOperation()
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            Block? block = consensusMechanism.GenerateNextBlock(ref stopWatch);

            stopWatch.Stop();

            if (block != null)
            {
                eventQueue.Enqueue(new CommunicationEvent(block, CommunicationEventType.SendBlock, null));
                eventQueue.Enqueue(new ConsensusEvent(null, ConsensusEventType.CreateTransaction, null));
                eventQueue.Enqueue(new DataCollectionEvent(NodeID, DataCollectionEventType.IncBlock, block));
            }
        }

        private ConsensusDriver InstantiateCorrespondingConsensusClass(int nodeID, int totalBlocksToCreate)
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var assemblyTypes = executingAssembly.GetTypes();
            var assemblyType = assemblyTypes.FirstOrDefault(x => x.Name == consensusType + "Consensus");

            if (assemblyType == null) throw new Exception("Was not able to instantiate any Consensus class.");

            var consensusCtor = assemblyType.GetConstructor(new[] { typeof(int), typeof(int) });

            if (consensusCtor == null) throw new Exception("Consensus class does not have the required constructor");

            return consensusCtor.Invoke(new object[] { nodeID, totalBlocksToCreate }) as ConsensusDriver ?? throw new Exception("Construction invokation failed");
        }

        private void HandleEventQueue()
        {
            if (!eventQueue.TryPeek(out var @event)) return;
            if (@event is not ConsensusEvent nextEvent) return;

            switch (nextEvent.EventType)
            {
                case ConsensusEventType.End:
                    break;
                case ConsensusEventType.CreateBlock:
                    break;
                case ConsensusEventType.RecieveBlock:
                    var blockWasAdded = consensusMechanism.RecieveBlock(nextEvent.Data as Block ?? throw new ArgumentException("String missing from event", nameof(nextEvent.Data)));
                    if (blockWasAdded)
                    {
                        eventQueue.Enqueue(new ConsensusEvent(null, ConsensusEventType.CreateTransaction, null));
                    }
                    break;
                case ConsensusEventType.CreateTransaction:
                    if (consensusMechanism.ExecutionFlag)
                    {
                        eventQueue.Enqueue(new CommunicationEvent(consensusMechanism.GenerateNextTransaction(), CommunicationEventType.SendTransaction, null));
                    }
                    break;
                case ConsensusEventType.RecieveTransaction:
                    consensusMechanism.RecieveTransaction(nextEvent.Data as Transaction ?? throw new ArgumentException("Transaction missing from event", nameof(nextEvent.Data)));
                    break;
                case ConsensusEventType.RequestBlockchain:
                    var blocks = consensusMechanism.RequestBlockChain();
                    if (blocks.Count > 0)
                    {
                        eventQueue.Enqueue(new CommunicationEvent(blocks, CommunicationEventType.RecieveBlockChain, nextEvent.Recipient as IPAddress ?? throw new ArgumentException("IPAddress missing from event", nameof(nextEvent.Recipient))));
                    }
                    else
                    {
                        Console.WriteLine($"A node requested my blockchain, but my blockchain is empty.");
                        consensusMechanism.BeginConsensus();
                    }
                    break;
                case ConsensusEventType.RecieveBlockchain:
                    consensusMechanism.RecieveBlockChain(nextEvent.Data as List<Block> ?? throw new ArgumentException("List<Block> missing from event", nameof(nextEvent.Data)));
                    break;
                default:
                    throw new ArgumentException("Unknown event type", nameof(nextEvent.EventType));
            }
            eventQueue.TryDequeue(out _);
        }

        private void NotifyModulesOfTestEnd()
        {
            if (consensusMechanism.ExecutionFlag == false)
            {
                eventQueue.Enqueue(new CommunicationEvent(null, CommunicationEventType.End, null));
                eventQueue.Enqueue(new DataCollectionEvent(NodeID, DataCollectionEventType.End, null));
                Console.WriteLine("Test is finished, modules has been signalled to stop.");
            }
        }
    }
}
