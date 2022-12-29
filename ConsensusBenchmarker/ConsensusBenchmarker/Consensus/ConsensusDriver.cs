using ConsensusBenchmarker.Extensions;
using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;
using ConsensusBenchmarker.Models.DTOs;
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
                    ExecutionFlag = false; // Triggers Shutdown of the whole node.
                }
            }
        }

        public int CreatedTransactionsByThisNode { get; private set; } = 0;

        public bool ExecutionFlag { get; private set; } = true;

        public List<Transaction> RecievedTransactionsSinceLastBlock { get; set; } = new();

        public List<Block> Blocks { get; set; } = new List<Block>();

        private readonly SemaphoreSlim recievedTransactionsSemaphore = new(1, 1);
        private readonly SemaphoreSlim blocksSemaphore = new(1, 1);
        private readonly int maxBlocksInChainAtOnce = 50;

        /// <summary>
        /// Tells the consensus to start.
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public virtual void BeginConsensus()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Handles the recieving of a new block from another node.
        /// </summary>
        /// <param name="serializedBlock"></param>
        /// <returns><see cref="bool"/></returns>
        public virtual bool RecieveBlock(BlockDTO block)
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

        /// <summary>
        /// Returns the nodes list of blocks that it currently has.
        /// </summary>
        /// <returns></returns>
        public virtual List<BlockDTO> RequestBlockChain()
        {
            return Blocks.Select(x => new BlockDTO(x, BlocksInChain)).ToList();
        }

        /// <summary>
        /// Empty the blockchain of all its blocks.
        /// </summary>
        public void EmptyBlockchain()
        {
            blocksSemaphore.Wait();

            Blocks.Clear();
            BlocksInChain = 0;

            blocksSemaphore.Release();
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

        /// <summary>
        /// Adds a new transaction thread safe.
        /// </summary>
        /// <param name="transaction"></param>
        public void AddNewTransaction(Transaction transaction)
        {
            recievedTransactionsSemaphore.Wait();

            RecievedTransactionsSinceLastBlock.Add(transaction);
            RecievedTransactionsSinceLastBlock = RecievedTransactionsSinceLastBlock.OrderBy(x => x.NodeID).ThenBy(x => x.TransactionId).ToList();

            recievedTransactionsSemaphore.Release();
        }

        /// <summary>
        /// Replace the last block in the chain with another block.
        /// </summary>
        /// <param name="previousBlock"></param>
        /// <param name="newBlock"></param>
        protected void ReplaceLastBlock(Block previousBlock, Block newBlock)
        {
            blocksSemaphore.Wait();

            Blocks.Remove(previousBlock);
            Blocks.Add(newBlock);

            blocksSemaphore.Release();
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

                Blocks.AddSorted(newBlock);
                BlocksInChain++;

                Console.WriteLine($"\nCD: Added block from owner: {newBlock.OwnerNodeID}, created at: {newBlock.BlockCreatedAt:HH:mm:ss}, current blocks in chain: {BlocksInChain}");

                MaintainBlockChainSize();

                blocksSemaphore.Release();

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
            recievedTransactionsSemaphore.Wait();

            List<Transaction> transactionsToBeRemoved = new();
            transactionsToBeRemoved.AddRange(RecievedTransactionsSinceLastBlock.Intersect(newBlock.Transactions));

            _ = RecievedTransactionsSinceLastBlock.RemoveAll(transactionsToBeRemoved.Contains);

            recievedTransactionsSemaphore.Release();
        }
    }
}
