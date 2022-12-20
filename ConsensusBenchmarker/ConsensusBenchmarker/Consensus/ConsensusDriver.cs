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
        public List<Models.Blocks.Block> Blocks { get; set; } = new List<Models.Blocks.Block>();

        private readonly Mutex recievedTransactionsMutex = new();
        private readonly Mutex blocksMutex = new();

        private readonly int maxBlocksInChainAtOnce = 10;

        /// <summary>
        /// Handles the recieving of a new block from another node.
        /// </summary>
        /// <param name="serializedBlock"></param>
        /// <returns><see cref="bool"/></returns>
        public virtual bool RecieveBlock(Models.Blocks.Block block)
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
            var newTransaction = new Transaction(NodeID, CreatedTransactionsByThisNode, DateTime.Now.ToLocalTime());
            CreatedTransactionsByThisNode++;
            return newTransaction;
        }

        /// <summary>
        /// Adds a new block to the chain as well as cleaning up the global chain and transactions.
        /// </summary>
        /// <param name="newBlock"></param>
        protected void AddNewBlockToChain(Models.Blocks.Block newBlock)
        {
            if (!Blocks.Contains(newBlock))
            {
                while (blocksMutex.WaitOne()) ;

                Blocks.Add(newBlock);
                MaintainBlockChainSize();

                blocksMutex.ReleaseMutex();

                RemoveNewBlockTransactions(newBlock);
            }
        }

        private void MaintainBlockChainSize()
        {
            if (Blocks.Count > maxBlocksInChainAtOnce)
            {
                Blocks.RemoveAt(0);
            }
        }

        private void RemoveNewBlockTransactions(Models.Blocks.Block newBlock)
        {
            while (recievedTransactionsMutex.WaitOne()) ;
            foreach (Transaction transaction in newBlock.Transactions)
            {
                _ = RecievedTransactionsSinceLastBlock.Remove(transaction);
            }
            recievedTransactionsMutex.ReleaseMutex();
        }
    }
}
