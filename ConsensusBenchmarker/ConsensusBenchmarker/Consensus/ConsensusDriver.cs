using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;
using System.Diagnostics;

namespace ConsensusBenchmarker.Consensus
{
    public abstract class ConsensusDriver
    {
        public int NodeID;
        public int CreatedTransactionsByThisNode { get; set; } = 0;
        public int TotalBlocksInChain { get; set; } = 0;
        public SortedList<(int, int), Transaction> RecievedTransactionsSinceLastBlock { get; set; } = new SortedList<(int, int), Transaction>();
        public List<Block> Blocks { get; set; } = new List<Block>();

        private readonly SemaphoreSlim recievedTransactionsSemaphore = new(1, 1);
        private readonly SemaphoreSlim blocksSemaphore = new(1, 1);

        private readonly int maxBlocksInChainAtOnce = 10;

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
        public virtual Block GenerateNextBlock(ref Stopwatch Stopwatch)
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
            RecievedTransactionsSinceLastBlock.Add((newTransaction.NodeID, newTransaction.TransactionId), newTransaction);
            CreatedTransactionsByThisNode++;
            return newTransaction;
        }

        public SortedList<(int, int), Transaction> CreateDeepCopyOfTransactions(SortedList<(int, int), Transaction> input)
        {
            SortedList<(int, int), Transaction> copySortedList = new SortedList<(int, int), Transaction>();
            foreach (KeyValuePair<(int, int), Transaction> pair in input)
            {
                copySortedList.Add(pair.Key, pair.Value.Clone());
            }
            return copySortedList;
        }

        /// <summary>
        /// Adds a new block to the chain as well as cleaning up the global chain and transactions.
        /// </summary>
        /// <param name="newBlock"></param>
        protected void AddNewBlockToChain(Block newBlock)
        {
            if (!Blocks.Contains(newBlock))
            {
                blocksSemaphore.Wait();

                Blocks.Add(newBlock);
                TotalBlocksInChain++;

                Console.WriteLine("CD: Added a new block to my chain. I am Node: " + NodeID + ". Block creator is: " + newBlock.OwnerNodeID + ". Current blocks in chain: " + TotalBlocksInChain);

                MaintainBlockChainSize();

                blocksSemaphore.Release();

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

        private void RemoveNewBlockTransactions(Block newBlock)
        {
            recievedTransactionsSemaphore.Wait();

            var transactionsToBeRemoved = RecievedTransactionsSinceLastBlock.Intersect(newBlock.Transactions).ToList();
            transactionsToBeRemoved.ForEach(t => RecievedTransactionsSinceLastBlock.Remove(t.Key));
            Console.WriteLine("CD: Number of current transactions after adding a new block and removing its transactions is: " + RecievedTransactionsSinceLastBlock.Count);

            recievedTransactionsSemaphore.Release();
        }
    }
}
