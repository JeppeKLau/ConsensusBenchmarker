using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;
using ConsensusBenchmarker.Models.Blocks.ConsensusBlocks;
using System.Security.Cryptography;
using System.Text;

namespace ConsensusBenchmarker.Consensus.PoW
{
    public class PoWConsensus : ConsensusDriver
    {
        public List<PoWBlock> Blocks { get; set; } = new List<PoWBlock>();

        private readonly uint DifficultyLeadingZeroes = 6;
        private bool allowMining = true;
        private bool restartMining = false;
        private readonly Random random;
        private readonly SHA256 sha256;

        public PoWConsensus(int nodeID)
        {
            NodeID = nodeID;
            sha256 = SHA256.Create();
            random = new Random(NodeID * new Random().Next());
        }

        public override bool RecieveBlock(Block block)
        {
            if (block is not PoWBlock recievedBlock)
            {
                throw new ArgumentException("Recieved block is not the correct type", nameof(block));
            }

            bool addBlock = false;
            PoWBlock? previousBlock = GetLastValidBlock();
            if (previousBlock == null) // Genesis Block
            {
                addBlock = true;
            }
            else
            {
                if (IsBlockValid(previousBlock, recievedBlock)) // Next Block
                {
                    addBlock = true;
                }
            }

            if (addBlock)
            {
                allowMining = false;
                AddNewBlock(recievedBlock);
            }
            allowMining = true;
            return addBlock;
        }

        public override void RecieveTransaction(Transaction transaction)
        {
            if (!RecievedTransactionsSinceLastBlock.Contains(transaction))
            {
                RecievedTransactionsSinceLastBlock.Add(transaction);
                restartMining = true;
            }
        }

        public override PoWBlock GenerateNextBlock()
        {
            while (!allowMining && RecievedTransactionsSinceLastBlock.Count != 0) ;
            return MineNewBlock() ?? GenerateNextBlock();
        }

        #region MineNewBlock

        private void AddNewBlock(PoWBlock block)
        {
            Blocks.Add(block);
            TotalBlocksInChain++;
            RemoveSpecificTransactions(block.Transactions);
        }

        private PoWBlock? MineNewBlock()
        {
            PoWBlock? previousBlock = GetLastValidBlock();
            string previousBlockHash = "";
            PoWBlock? newBlock = null;
            int nonce;
            restartMining = false;

            if (previousBlock != null)
            {
                previousBlockHash = previousBlock.BlockHash;
            }

            RecievedTransactionsSinceLastBlock = RecievedTransactionsSinceLastBlock.OrderBy(x => x.NodeID).ToList();
            string transactionsAsString = string.Join(",", RecievedTransactionsSinceLastBlock.Select(x => x.ToString()));
            byte[] encodedTransactions = Encoding.UTF8.GetBytes(transactionsAsString);
            byte[] previousBlockHashInBytes = Encoding.UTF8.GetBytes(previousBlockHash);
            byte[] previousHashAndTransactions = CombineByteArrays(previousBlockHashInBytes, encodedTransactions);

            while (allowMining && newBlock == null)
            {
                if (restartMining) return null;

                nonce = random.Next(0, int.MaxValue);
                string blockHash = HashNewBlock(previousHashAndTransactions, nonce);
                if (HashConformsToDifficulty(blockHash))
                {
                    newBlock = new PoWBlock(NodeID, DateTime.Now.ToLocalTime(), RecievedTransactionsSinceLastBlock, blockHash, previousBlockHash, nonce);
                    AddNewBlock(newBlock);
                }
            }
            return newBlock;
        }

        private string HashNewBlock(byte[] previousHashAndTransactions, int nonce)
        {
            byte[] encodedNonce = Encoding.UTF8.GetBytes(nonce.ToString());
            byte[] wholeBlock = CombineByteArrays(previousHashAndTransactions, encodedNonce);
            byte[] byteHash = sha256.ComputeHash(wholeBlock);
            return Convert.ToHexString(byteHash);
        }

        private byte[] CombineByteArrays(byte[] first, byte[] second)
        {
            byte[] ret = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, ret, 0, first.Length);
            Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
            return ret;
        }

        private bool HashConformsToDifficulty(string hash)
        {
            bool valid = true;
            for (int index = 0; index < hash.Length && index < DifficultyLeadingZeroes; index++)
            {
                if (hash[index] != '0')
                {
                    valid = false;
                    break;
                }
            }
            return valid;
        }

        #endregion

        #region ValidateBlock

        /// <summary>
        /// Determines whether or not a new block is valid to be added as the next block in the chain. Throws exception if the current chain is empty.
        /// </summary>
        /// <param name="newBlock"></param>
        /// <returns></returns>
        private bool IsBlockValid(PoWBlock previousBlock, PoWBlock newBlock)
        {
            if (Blocks.Count == 0) throw new Exception("The current chain is empty and a new block can therefor not be validated.");

            if (previousBlock.BlockHash.Equals(newBlock.PreviousBlockHash) && ValidateNewBlockHash(previousBlock, newBlock))
            {
                return true;
            }
            return false;
        }

        private bool ValidateNewBlockHash(PoWBlock previousBlock, PoWBlock newBlock)
        {
            RecievedTransactionsSinceLastBlock = RecievedTransactionsSinceLastBlock.OrderBy(x => x.NodeID).ToList();
            string transactionsAsString = string.Join(",", RecievedTransactionsSinceLastBlock.Select(x => x.ToString()));
            byte[] encodedTransactions = Encoding.UTF8.GetBytes(transactionsAsString);
            byte[] previousBlockHashInBytes = Encoding.UTF8.GetBytes(previousBlock.BlockHash);
            byte[] previousHashAndTransactions = CombineByteArrays(previousBlockHashInBytes, encodedTransactions);

            string newBlocksHash = HashNewBlock(previousHashAndTransactions, newBlock.Nonce);
            if (HashConformsToDifficulty(newBlocksHash))
            {
                return true;
            }
            return false;
        }

        #endregion

        private void RemoveSpecificTransactions(List<Transaction> shouldBeRemoved)
        {
            foreach (Transaction transaction in shouldBeRemoved)
            {
                _ = RecievedTransactionsSinceLastBlock.Remove(transaction);
            }
        }

        private PoWBlock? GetLastValidBlock()
        {
            if (Blocks.Count > 0)
            {
                return Blocks.Last();
            }
            return null;
        }

    }
}
