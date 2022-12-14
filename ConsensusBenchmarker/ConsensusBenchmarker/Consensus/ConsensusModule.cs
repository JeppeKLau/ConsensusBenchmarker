using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;
using ConsensusBenchmarker.Models.Events;
using Newtonsoft.Json;
using System.Reflection;

namespace ConsensusBenchmarker.Consensus
{
    public class ConsensusModule
    {
        private readonly string consensusType;
        private ConsensusDriver consensusMechanism;
        private readonly Stack<IEvent> eventStack;
        private bool executionFlag;
        private int totalBlocksToCreate = 0;

        public ConsensusModule(string consensusType, int totalBlocksToCreate, int nodeID, ref Stack<IEvent> eventStack)
        {
            this.totalBlocksToCreate = totalBlocksToCreate;
            this.consensusType = consensusType;
            consensusMechanism = InstantiateCorrespondingConsensusClass(nodeID);
            this.eventStack = eventStack;
        }

        public async Task RunConsensus()
        {
            eventStack.Push(new ConsensusEvent(null, ConsensusEventType.CreateTransaction));
            var miningTask = new Task(() => HandleMiningOperation());
            var eventTask = new Task(async () => { while (executionFlag) await HandleEventStack(); });

            await Task.WhenAll(miningTask, eventTask);
        }

        private Task HandleMiningOperation()
        {
            while (executionFlag && consensusMechanism.TotalBlocksInChain < totalBlocksToCreate)
            {
                Block block = consensusMechanism.GenerateNextBlock();
                eventStack.Push(new CommunicationEvent(block, CommunicationEventType.SendBlock));
                eventStack.Push(new ConsensusEvent(null, ConsensusEventType.CreateTransaction));
            }
            executionFlag = false;
            return Task.CompletedTask;
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

        private async Task HandleEventStack()
        {
            if (eventStack.Peek() is not ConsensusEvent nextEvent) return;

            switch (nextEvent.EventType)
            {
                case ConsensusEventType.End:
                    executionFlag = false;
                    break;
                case ConsensusEventType.CreateBlock:
                    break;
                case ConsensusEventType.RecieveBlock:
                    var valid = consensusMechanism.RecieveBlock(nextEvent.Data as Block ?? throw new ArgumentException("String missing from event", nameof(nextEvent.Data)));
                    if (valid)
                    {
                        eventStack.Push(new ConsensusEvent(null, ConsensusEventType.CreateTransaction));
                    }
                    break;
                case ConsensusEventType.CreateTransaction:
                    eventStack.Push(new CommunicationEvent(consensusMechanism.GenerateNextTransaction(), CommunicationEventType.SendTransaction));
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
            eventStack.Pop();
        }
    }
}
