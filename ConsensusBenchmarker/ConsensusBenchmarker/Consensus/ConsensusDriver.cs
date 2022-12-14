using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;

namespace ConsensusBenchmarker.Consensus
{
    public abstract class ConsensusDriver
    {
        public int NodeID;
        public int CreatedTransactionsByThisNode { get; set; } = 0;
        public int TotalBlocksInChain { get; set; } = 0;
        public List<Transaction> RecievedTransactionsSinceLastBlock { get; set; } = new List<Transaction>();

        /// <summary>
        /// Handles the recieving of a new block from another node.
        /// </summary>
        /// <param name="serializedBlock"></param>
        /// <returns><see cref="bool"/></returns>
        public virtual bool RecieveBlock(Block block)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Handles the recieving of a new transaction from another node.
        /// </summary>
        /// <param name="transaction"></param>
        public virtual void RecieveTransaction(Transaction transaction)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Generates and returns a valid genesis block.
        /// </summary>
        /// <returns><see cref="Block"/></returns>
        public virtual Block GenerateGenesisBlock()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Generates and returns a new valid block.
        /// </summary>
        /// <returns><see cref="Block"/></returns>
        public virtual Block GenerateNextBlock()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Generate and return a new transaction.
        /// </summary>
        /// <returns><see cref="Transaction"/></returns>
        public Transaction GenerateNextTransaction()
        {
            Transaction newTransaction = new Transaction(NodeID, CreatedTransactionsByThisNode, DateTime.Now.ToLocalTime());
            CreatedTransactionsByThisNode++;
            return newTransaction;
        }

    }
}
