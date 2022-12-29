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

        public override bool RecieveBlock(Block newBlock)
        {
            if (newBlock is not PoWBlock newPoWBlock)
            {
                throw new ArgumentException("Recieved block is not the correct type", newBlock.GetType().FullName);
            }

            bool isBlockValid;
            PoWBlock? previousBlock = GetLastValidBlock();

            if (previousBlock == null)
            {
                isBlockValid = ValidateNewBlockHash(newPoWBlock);
            }
            else
            {
                isBlockValid = previousBlock.BlockHash.Equals(newPoWBlock.PreviousBlockHash) && ValidateNewBlockHash(newPoWBlock);
            }

            if (isBlockValid)
            {
                AddNewBlockToChain(newPoWBlock);
                return true;
            }
            else if (newPoWBlock.BlockCreatedAt < previousBlock!.BlockCreatedAt && newPoWBlock.PreviousBlockHash.Equals(previousBlock.PreviousBlockHash))
            {
                Console.WriteLine($"PoW: HotSwap. replaced block ({previousBlock.OwnerNodeID}, {previousBlock.BlockCreatedAt:o}), with block ({newPoWBlock.OwnerNodeID}, {newPoWBlock.BlockCreatedAt:o}).");
                ReplaceLastBlock(previousBlock, newPoWBlock);
            }
            Console.WriteLine($"The block from {newPoWBlock.OwnerNodeID} created at {newPoWBlock.BlockCreatedAt} was NOT valid.");
            return false;
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

        private PoWBlock? GetLastValidBlock()
        {
            if (Blocks.Count > 0)
            {
                return Blocks.Last() as PoWBlock;
            }
            return null;
        }

        #region Mining

        private PoWBlock? MineNewBlock(ref Stopwatch stopwatch)
        {
            PoWBlock? previousBlock = GetLastValidBlock();
            string previousBlockHash = previousBlock?.BlockHash ?? string.Empty;
            PoWBlock? newBlock = null;
            long nonce;
            restartMining = false;

            List<Transaction> currentTransactionsCopy = RecievedTransactionsSinceLastBlock.ToList();
            byte[] previousHashAndTransactions = GetPreviousHashAndTransactionByteArray(previousBlockHash, currentTransactionsCopy);

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
                        newBlock = new PoWBlock(NodeID, DateTime.UtcNow, currentTransactionsCopy, blockHash, previousBlockHash, nonce);
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
            int retLength = -1;
            try
            {
                byte[] ret = new byte[first.Length + second.Length];
                retLength = ret.Length; // TEMP
                first.CopyTo(ret, 0);
                second.CopyTo(ret, first.Length);
                return ret;
            }
            catch { }
            throw new Exception($"Copy byte array failed. First array length: {first.Length}, second: {second.Length}, combined: {retLength}");

            // Only got this exception ONE time
            // Unhandled exception. System.ArgumentException: Destination array was not long enough. Check the destination index, length, and the array's lower bounds. (Parameter 'destinationArray')
            // at System.Array.Copy(Array sourceArray, Int32 sourceIndex, Array destinationArray, Int32 destinationIndex, Int32 length, Boolean reliable)
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

    }
}
