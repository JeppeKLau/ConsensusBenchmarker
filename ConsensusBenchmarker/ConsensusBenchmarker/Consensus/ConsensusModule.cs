using ConsensusBenchmarker.Models;
using System.Reflection;

namespace ConsensusBenchmarker.Consensus
{
    public class ConsensusModule
    {
        public int createdTransactionsByThisNode { get; set; } = 0;
        public int totalBlocksInChain { get; set; } = 0;
        public List<Transaction> RecievedTransactionsSicnceLastBlock { get; set; } = new List<Transaction>();

        private readonly string consensusType;
        private IConsensus ConsensusMechanism;

        public ConsensusModule(string consensus)
        {
            consensusType = consensus;
            ConsensusMechanism = InstantiateCorrectConsensusClass();
        }

        private IConsensus InstantiateCorrectConsensusClass()
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var assemblyTypes = executingAssembly.GetTypes().Where(t => t.GetInterface(nameof(IConsensus)) != null).ToList();
            foreach (Type type in assemblyTypes)
            {
                if (type.Name.ToLower().Equals(consensusType.ToLower() + "consensus"))
                {
                    return executingAssembly.CreateInstance(type.FullName ?? "") as IConsensus ?? throw new Exception($"Unknown IConsensus assembly: {type.FullName ?? ""}");
                }
            }
            throw new Exception("Was not able to instantiate any Consensus class.");
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
