using ConsensusBenchmarker.Models;

namespace ConsensusBenchmarker.Consensus
{
    public abstract class ConsensusDriver
    {
        public int NodeID;
        public int CreatedTransactionsByThisNode { get; set; } = 0;
        public int TotalBlocksInChain { get; set; } = 0;
        public List<Transaction> RecievedTransactionsSicnceLastBlock { get; set; } = new List<Transaction>();

        public virtual void RecieveTransaction(Transaction transaction)
        {
            throw new NotImplementedException();
        }

        public virtual void RecieveBlock()
        {
            throw new NotImplementedException();
        }



    }
}
