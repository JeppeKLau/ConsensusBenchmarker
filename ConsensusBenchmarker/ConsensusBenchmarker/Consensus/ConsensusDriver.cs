using ConsensusBenchmarker.Extensions;
using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;
using System.Diagnostics;

namespace ConsensusBenchmarker.Consensus
{
    public abstract class ConsensusDriver
    {
        protected ConsensusDriver(int nodeID, int maxBlocksToCreate)
        {
            NodeID = nodeID;
            MaxBlocksToCreate = maxBlocksToCreate;
        }

        public readonly int NodeID;

        public readonly int MaxBlocksToCreate;

        private int blocksInChain = 0;
        public int BlocksInChain
        {
            get => blocksInChain;
            set
            {
                blocksInChain = value;
                if (blocksInChain >= MaxBlocksToCreate)
                {
                    ExecutionFlag = false; // Triggers Shutdown of whole node.
                }
            }
        }

        public int CreatedTransactionsByThisNode { get; private set; } = 0;

        public bool ExecutionFlag { get; private set; } = true;

        public List<Transaction> RecievedTransactionsSinceLastBlock { get; set; } = new();

        public List<Block> Blocks { get; set; } = new List<Block>();

        private readonly SemaphoreSlim recievedTransactionsMutex = new(1, 1);
        private readonly SemaphoreSlim blocksMutex = new(1, 1);
        private readonly int maxBlocksInChainAtOnce = 10;

        public virtual void BeginConsensus()
        {
            throw new NotImplementedException();
        }

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
        public virtual Block? GenerateNextBlock(ref Stopwatch Stopwatch)
        {
            throw new NotImplementedException();
        }

        public virtual List<Block> RequestBlockChain()
        {
            return Blocks;
        }

        public virtual void RecieveBlockChain(List<Block>? blocks)
        {
            // This node could, if its lucky, be able to add its own block while waiting for a response for another node's blockchain, just fyi
            //      The fact this comment exists, implies the implemented solution is not sound - j
            if (blocks is null)
            {
                return;
            }

            foreach (Block block in blocks)
            {
                RecieveBlock(block);
            }
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

                Blocks.AddSorted(newBlock);
                BlocksInChain++;

                Console.WriteLine($"\nCD: Added block from owner: {newBlock.OwnerNodeID}, created at: {newBlock.BlockCreatedAt:HH:mm:ss}, current blocks in chain: {BlocksInChain}");

                MaintainBlockChainSize();

                blocksMutex.Release();

                RemoveNewBlockTransactions(newBlock);

                Console.WriteLine($"CD: Current transactions after adding a new block is: {RecievedTransactionsSinceLastBlock.Count}.\n");
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

            _ = RecievedTransactionsSinceLastBlock.RemoveAll(transactionsToBeRemoved.Contains);

            recievedTransactionsMutex.Release();
        }
    }
}
