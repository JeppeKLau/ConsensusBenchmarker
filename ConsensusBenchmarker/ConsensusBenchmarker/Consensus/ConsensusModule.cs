using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;
using ConsensusBenchmarker.Models.Events;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace ConsensusBenchmarker.Consensus
{
    public class ConsensusModule
    {
        private readonly string consensusType;
        private readonly ConsensusDriver consensusMechanism;
        private readonly ConcurrentQueue<IEvent> eventQueue;
        private bool executionFlag;
        private readonly int totalBlocksToCreate = 0;
        private readonly int NodeID;

        public ConsensusModule(string consensusType, int totalBlocksToCreate, int nodeID, ref ConcurrentQueue<IEvent> eventQueue)
        {
            this.totalBlocksToCreate = totalBlocksToCreate;
            this.consensusType = consensusType;
            consensusMechanism = InstantiateCorrespondingConsensusClass(nodeID);
            this.eventQueue = eventQueue;
            NodeID = nodeID;
            executionFlag = true;
        }

        public List<Thread> SpawnThreads()
        {
            eventQueue.Enqueue(new ConsensusEvent(null, ConsensusEventType.CreateTransaction));
            var consensusThreads = new List<Thread>
            {
                new Thread(() =>
                {
                    while (executionFlag)
                    {
                        HandleEventQueue();
                    }
                })
            };

            if (consensusType == "PoW") // Could probably be prettier
            {
                consensusThreads.Add(new Thread(HandleMiningOperation));
            }
            return consensusThreads;
        }

        private void HandleMiningOperation()
        {
            while (executionFlag)
            {
                var stopWatch = new Stopwatch();
                stopWatch.Start();

                Block block = consensusMechanism.GenerateNextBlock(ref stopWatch);

                stopWatch.Stop();
                Console.WriteLine($"CM: It took {stopWatch.Elapsed.Seconds} seconds to mine the new block.");

                eventQueue.Enqueue(new CommunicationEvent(block, CommunicationEventType.SendBlock)); // should another node validate a newly found block before this node adds it to its chain and creates a new transaction?
                eventQueue.Enqueue(new ConsensusEvent(null, ConsensusEventType.CreateTransaction));
                eventQueue.Enqueue(new DataCollectionEvent(NodeID, DataCollectionEventType.IncBlock, block));
            }
        }

        private ConsensusDriver InstantiateCorrespondingConsensusClass(int nodeID)
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var assemblyTypes = executingAssembly.GetTypes();
            var assemblyType = assemblyTypes.FirstOrDefault(x => x.Name == consensusType + "Consensus");

            if (assemblyType == null) throw new Exception("Was not able to instantiate any Consensus class.");

            var consensusCtor = assemblyType.GetConstructor(new[] { typeof(int) });

            if (consensusCtor == null) throw new Exception("Consensus class does not have the required constructor");

            return consensusCtor.Invoke(new object[] { nodeID }) as ConsensusDriver ?? throw new Exception("Construction invokation failed");
        }

        private void HandleEventQueue()
        {
            if (consensusMechanism.TotalBlocksInChain >= totalBlocksToCreate)
            {
                eventQueue.Enqueue(new CommunicationEvent(null, CommunicationEventType.End));
                eventQueue.Enqueue(new DataCollectionEvent(NodeID, DataCollectionEventType.End, null));
                executionFlag = false;
                Console.WriteLine("Test finished, saving data.");
            }

            if (!eventQueue.TryPeek(out var @event)) return;
            if (@event is not ConsensusEvent nextEvent) return;

            switch (nextEvent.EventType)
            {
                case ConsensusEventType.End:
                    break;
                case ConsensusEventType.CreateBlock:
                    break;
                case ConsensusEventType.RecieveBlock:
                    var valid = consensusMechanism.RecieveBlock(nextEvent.Data as Block ?? throw new ArgumentException("String missing from event", nameof(nextEvent.Data)));
                    if (valid)
                    {
                        eventQueue.Enqueue(new ConsensusEvent(null, ConsensusEventType.CreateTransaction));
                    }
                    break;
                case ConsensusEventType.CreateTransaction:
                    eventQueue.Enqueue(new CommunicationEvent(consensusMechanism.GenerateNextTransaction(), CommunicationEventType.SendTransaction));
                    break;
                case ConsensusEventType.RecieveTransaction:
                    consensusMechanism.RecieveTransaction(nextEvent.Data as Transaction ?? throw new ArgumentException("Transaction missing from event", nameof(nextEvent.Data)));
                    break;
                case ConsensusEventType.RequestBlockchain:
                    break;
                case ConsensusEventType.RecieveBlockchain:
                    break;
                default:
                    throw new ArgumentException("Unknown event type", nameof(nextEvent.EventType));
            }
            eventQueue.TryDequeue(out _);
        }
    }
}
