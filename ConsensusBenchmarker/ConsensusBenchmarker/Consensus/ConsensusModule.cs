using ConsensusBenchmarker.Models;
using System.Reflection;

namespace ConsensusBenchmarker.Consensus
{
    public class ConsensusModule
    {
        private readonly string consensusType;
        private ConsensusDriver ConsensusMechanism;

        public ConsensusModule(string consensusType, int nodeID)
        {
            this.consensusType = consensusType;
            ConsensusMechanism = InstantiateCorrespondingConsensusClass(nodeID);
        }

        private ConsensusDriver InstantiateCorrespondingConsensusClass(int nodeID)
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var assemblyType = executingAssembly.GetType(consensusType + "Consensus");

            if (assemblyType == null) throw new Exception("Was not able to instantiate any Consensus class.");

            var consensusCtor = assemblyType.GetConstructor(new[] { typeof(int) });

            if (consensusCtor == null) throw new Exception("Consensus class does not have the required constructor");

            return consensusCtor.Invoke(new object[] { nodeID }) as ConsensusDriver ?? throw new Exception("Construction invokation failed");
        }

        public void RecieveTransaction(Transaction transaction)
        {
            ConsensusMechanism.RecieveTransaction(transaction);
        }

        public void RecieveBlock()
        {
            ConsensusMechanism.RecieveBlock();
        }


    }
}
