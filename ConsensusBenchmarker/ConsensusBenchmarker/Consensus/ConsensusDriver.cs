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

        public List<Transaction> RecievedTransactionsSinceLastBlock { get; set; } = new();

        public List<Block> Blocks { get; set; } = new List<Block>();

        private readonly SemaphoreSlim recievedTransactionsMutex = new(1, 1);
        private readonly SemaphoreSlim blocksMutex = new(1, 1);
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
            AddNewTransaction(newTransaction);
            CreatedTransactionsByThisNode++;
            return newTransaction;
        }

        public void AddNewTransaction(Transaction transaction)
        {
            RecievedTransactionsSinceLastBlock.Add(transaction);
            RecievedTransactionsSinceLastBlock = RecievedTransactionsSinceLastBlock.OrderBy(x => x.NodeID).ThenBy(x => x.TransactionId).ToList();
        }

        /// <summary>
        /// Adds a new block to the chain as well as cleaning up the global chain and transactions.
        /// </summary>
        /// <param name="newBlock"></param>
        protected void AddNewBlockToChain(Block newBlock)
        {
            if (!Blocks.Contains(newBlock))
            {
                blocksMutex.Wait();

                Blocks.Add(newBlock);
                TotalBlocksInChain++;


                Console.WriteLine("CD: Added a new block to my chain. I am Node: " + NodeID + ". Block creator is: " + newBlock.OwnerNodeID + ". Current blocks in chain: " + TotalBlocksInChain);

                MaintainBlockChainSize();

                blocksMutex.Release();

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
            recievedTransactionsMutex.Wait();

            List<Transaction> transactionsToBeRemoved = new();
            transactionsToBeRemoved.AddRange(RecievedTransactionsSinceLastBlock.Intersect(newBlock.Transactions));

            Console.WriteLine("New block transactions:");
            newBlock.Transactions.ForEach(t => Console.Write($"({t.NodeID}, {t.TransactionId}) "));
            Console.WriteLine();

            Console.WriteLine("Transactions since last block:");
            RecievedTransactionsSinceLastBlock.ForEach(t => Console.Write($"({t.NodeID}, {t.TransactionId}) "));
            Console.WriteLine();

            Console.WriteLine("Transaction to be removed:");
            transactionsToBeRemoved.ForEach(t => Console.Write($"({t.NodeID}, {t.TransactionId}) "));
            Console.WriteLine();

            _ = RecievedTransactionsSinceLastBlock.RemoveAll(transactionsToBeRemoved.Contains);

            Console.WriteLine("CD: Number of current transactions after adding a new block and removing its transactions is: " + RecievedTransactionsSinceLastBlock.Count);

            recievedTransactionsMutex.Release();
        }
    }
}
