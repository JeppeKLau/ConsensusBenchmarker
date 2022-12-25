using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;
using ConsensusBenchmarker.Models.Blocks.ConsensusBlocks;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace ConsensusBenchmarker.Consensus.PoW
{
    public class PoWConsensus : ConsensusDriver
    {
        private readonly uint DifficultyLeadingZeroes;
        private volatile bool allowMining;
        private volatile bool restartMining;
        private readonly Random random;

        public PoWConsensus(int nodeID, int maxBlocksToCreate) : base(nodeID, maxBlocksToCreate)
        {
            random = new Random(NodeID * new Random().Next());
            allowMining = false;
            restartMining = false;
            DifficultyLeadingZeroes = uint.Parse(Environment.GetEnvironmentVariable("POW_DIFFICULTY") ?? "3"); // 3 for testing
        }

        public override void BeginConsensus()
        {
            allowMining = true;
        }

        public override bool RecieveBlock(Block block)
        {
            if (block is not PoWBlock recievedBlock)
            {
                throw new ArgumentException("Recieved block is not the correct type", block.GetType().FullName);
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
                AddNewBlockToChain(block);
            }
            allowMining = true;
            return addBlock;
        }

        public override void RecieveTransaction(Transaction transaction)
        {
            if (!RecievedTransactionsSinceLastBlock.Any(x => x.Equals(transaction)))
            {
                AddNewTransaction(transaction);
                restartMining = true;
            }
        }

        public override PoWBlock? GenerateNextBlock(ref Stopwatch Stopwatch)
        {
            if (ExecutionFlag)
            {
                while (!allowMining || RecievedTransactionsSinceLastBlock.Count == 0)
                {
                    if (ExecutionFlag == false) break;
                    Thread.Sleep(1);
                }

                PoWBlock? miningResult = MineNewBlock(ref Stopwatch);
                if (miningResult == null)
                {
                    return GenerateNextBlock(ref Stopwatch);
                }
                return miningResult;
            }
            return null;
        }

        public override void RecieveBlockChain(List<Block>? blocks)
        {
            if (blocks is null)
            {
                allowMining = true;
                return;
            }
            if (blocks.Count > 0 && Blocks.Count == 0)
            {
                Console.WriteLine($"PoW: Recieved a requested blockchain with {Blocks.Count} in it, will now validate and add them.");
                foreach (Block block in blocks)
                {
                    RecieveBlock(block);
                }
            }
        }

        #region MineNewBlock

        private PoWBlock? MineNewBlock(ref Stopwatch stopwatch)
        {
            PoWBlock? previousBlock = GetLastValidBlock();
            string previousBlockHash = previousBlock?.BlockHash ?? string.Empty;
            PoWBlock? newBlock = null;
            long nonce;
            restartMining = false;

            byte[] previousHashAndTransactions = GetPreviousHashAndTransactionByteArray(previousBlockHash, RecievedTransactionsSinceLastBlock.ToList());

            using (SHA256 sha256 = SHA256.Create())
            {
                while (newBlock == null)
                {
                    if (restartMining || allowMining == false || ExecutionFlag == false)
                    {
                        stopwatch.Restart();
                        return null;
                    }

                    nonce = random.NextInt64(0, int.MaxValue);
                    string blockHash = HashNewBlock(sha256, previousHashAndTransactions, nonce);
                    if (HashConformsToDifficulty(blockHash))
                    {
                        newBlock = new PoWBlock(NodeID, DateTime.UtcNow, RecievedTransactionsSinceLastBlock.ToList(), blockHash, previousBlockHash, nonce);
                        AddNewBlockToChain(newBlock);
                    }
                }
            }
            return newBlock;
        }

        private static byte[] GetPreviousHashAndTransactionByteArray(string previousBlockHash, List<Transaction> transactions)
        {
            var transactionsAsString = string.Join(",", transactions);
            return Encoding.UTF8.GetBytes(previousBlockHash + transactionsAsString);
        }

        private static string HashNewBlock(SHA256 sha256, byte[] previousHashAndTransactions, long nonce)
        {
            byte[] encodedNonce = Encoding.UTF8.GetBytes(nonce.ToString());
            byte[] wholeBlock = CombineByteArrays(previousHashAndTransactions, encodedNonce);
            byte[] byteHash = sha256.ComputeHash(wholeBlock);
            return Convert.ToHexString(byteHash);
        }

        private static byte[] CombineByteArrays(byte[] first, byte[] second)
        {
            byte[] ret = new byte[first.Length + second.Length];
            first.CopyTo(ret, 0);
            second.CopyTo(ret, first.Length);
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
        /// <param name="previousBlock"></param>
        /// <param name="newBlock"></param>
        /// <returns><see cref="bool"/></returns>
        /// <exception cref="Exception"></exception>
        private bool IsBlockValid(PoWBlock previousBlock, PoWBlock newBlock)
        {
            if (Blocks.Count == 0) throw new Exception("The current chain is empty and a new block can therefore not be validated.");

            if (previousBlock.BlockHash.Equals(newBlock.PreviousBlockHash) && ValidateNewBlockHash(newBlock))
            {
                return true;
            }
            Console.WriteLine($"The block from {newBlock.OwnerNodeID} created at {newBlock.BlockCreatedAt} was NOT valid.");
            return false;
        }

        private bool ValidateNewBlockHash(PoWBlock newBlock)
        {
            byte[] previousHashAndTransactions = GetPreviousHashAndTransactionByteArray(newBlock.PreviousBlockHash, newBlock.Transactions);

            using SHA256 sha256 = SHA256.Create();
            string newBlocksHash = HashNewBlock(sha256, previousHashAndTransactions, newBlock.Nonce);

            if (HashConformsToDifficulty(newBlocksHash) && newBlock.BlockHash.Equals(newBlocksHash))
            {
                return true;
            }
            return false;
        }

        #endregion

        private PoWBlock? GetLastValidBlock()
        {
            if (Blocks.Count > 0)
            {
                return Blocks.Last() as PoWBlock;
            }
            return null;
        }
    }
}
