﻿using ConsensusBenchmarker.Models;
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
        private bool requestBlockcHainHasHappened;

        public ConsensusModule(string consensusType, int maxBlocksToCreate, int nodeID, ref ConcurrentQueue<IEvent> eventQueue)
        {
            this.consensusType = consensusType;
            consensusMechanism = InstantiateCorrespondingConsensusClass(nodeID, maxBlocksToCreate);
            this.eventQueue = eventQueue;
            NodeID = nodeID;
            requestBlockcHainHasHappened = false;
        }

        public void SpawnThreads(Dictionary<string, Thread> moduleThreads)
        {
            eventQueue.Enqueue(new CommunicationEvent(null, CommunicationEventType.RequestBlockChain, null));
            eventQueue.Enqueue(new ConsensusEvent(null, ConsensusEventType.CreateTransaction, null));
            moduleThreads.Add("Consensus_HandleEventLoop", new Thread(() =>
            {
                while (consensusMechanism.ExecutionFlag || !eventQueue.IsEmpty)
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
                while (eventQueue.Any(e => e is ConsensusEvent consensusEvent && consensusEvent.EventType == ConsensusEventType.RecieveBlock)) // Recieve all blocks in the queue before trying to add your own block.
                {
                    Thread.Sleep(1);
                }

                if (consensusMechanism.RecieveBlock(block, ref stopWatch))
                {
                    eventQueue.Enqueue(new CommunicationEvent(block, CommunicationEventType.SendBlock, null));
                    eventQueue.Enqueue(new ConsensusEvent(null, ConsensusEventType.CreateTransaction, null));
                    eventQueue.Enqueue(new DataCollectionEvent(NodeID, DataCollectionEventType.IncBlock, stopWatch));
                }
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
                    var newBlock = nextEvent.Data as Block ?? throw new ArgumentException("Block missing from event", nameof(nextEvent.Data));
                    if (requestBlockcHainHasHappened)
                    {
                        var blockStopwatch = new Stopwatch();
                        var blockWasAdded = consensusMechanism.RecieveBlock(newBlock, ref blockStopwatch);
                        if (blockWasAdded)
                        {
                            eventQueue.Enqueue(new ConsensusEvent(null, ConsensusEventType.CreateTransaction, null));
                            eventQueue.Enqueue(new DataCollectionEvent(NodeID, DataCollectionEventType.IncBlock, blockStopwatch));
                        }
                    }
                    else
                    {
                        Console.WriteLine("Received block before I heard back from my requestblockchain, requeuing.");
                        eventQueue.Enqueue(new ConsensusEvent(newBlock, ConsensusEventType.RecieveBlock, null));
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
                    eventQueue.Enqueue(new CommunicationEvent(consensusMechanism.RequestBlockChain(), CommunicationEventType.RecieveBlockChain, nextEvent.Recipient as IPAddress ?? throw new ArgumentException("IPAddress missing from event", nameof(nextEvent.Recipient))));
                    break;
                case ConsensusEventType.RecieveBlockchain:
                    var blockChain = nextEvent.Data as List<Block> ?? throw new ArgumentException("List<Block> missing from event", nameof(nextEvent.Data));
                    RecieveBlockChain(blockChain);
                    consensusMechanism.BeginConsensus();
                    requestBlockcHainHasHappened = true;
                    break;
                default:
                    throw new ArgumentException("Unknown event type", nameof(nextEvent.EventType));
            }
            eventQueue.TryDequeue(out _);
        }

        private void RecieveBlockChain(List<Block> blockChain)
        {
            var blockChainStopwatch = new Stopwatch();

            foreach (var block in blockChain)
            {
                consensusMechanism.RecieveBlock(block, ref blockChainStopwatch);
                eventQueue.Enqueue(new DataCollectionEvent(NodeID, DataCollectionEventType.IncBlock, blockChainStopwatch));
                blockChainStopwatch.Reset();
            }
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
