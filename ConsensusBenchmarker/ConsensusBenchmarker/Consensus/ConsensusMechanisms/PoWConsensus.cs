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
        private readonly uint DifficultyLeadingZeroes = 6;
        private bool allowMining;
        private bool restartMining;
        private bool validateBlock;
        private readonly Random random;
        private readonly SHA256 sha256;
        private SemaphoreSlim consoleSemaphore = new(1, 1);

        public PoWConsensus(int nodeID)
        {
            NodeID = nodeID;
            sha256 = SHA256.Create();
            random = new Random(NodeID * new Random().Next());
            allowMining = true;
            restartMining = false;
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

        public override PoWBlock GenerateNextBlock(ref Stopwatch Stopwatch)
        {
            while (!allowMining || RecievedTransactionsSinceLastBlock.Count == 0) ;

            return MineNewBlock(ref Stopwatch) ?? GenerateNextBlock(ref Stopwatch);
        }

        #region MineNewBlock

        private PoWBlock? MineNewBlock(ref Stopwatch stopwatch)
        {
            PoWBlock? previousBlock = GetLastValidBlock();
            string previousBlockHash = previousBlock?.BlockHash ?? string.Empty;
            PoWBlock? newBlock = null;
            long nonce;
            restartMining = false;

            byte[] previousHashAndTransactions = GetPreviousHashAndTransactionByteArray(previousBlockHash, RecievedTransactionsSinceLastBlock);

            while (newBlock == null)
            {
                if (restartMining || allowMining == false)
                {
                    stopwatch.Restart();
                    return null;
                }

                nonce = random.NextInt64(0, int.MaxValue);
                var (blockByteArray, wholeBlock) = HashNewBlock(previousHashAndTransactions, nonce);
                string blockHash = Convert.ToHexString(blockByteArray);
                if (HashConformsToDifficulty(blockHash))
                {
                    newBlock = new PoWBlock(NodeID, DateTime.Now.ToLocalTime(), RecievedTransactionsSinceLastBlock.ToList(), blockHash, previousBlockHash, nonce);
                    AddNewBlockToChain(newBlock);

                    consoleSemaphore.Wait();
                    Console.WriteLine();
                    Console.WriteLine("New block mined ({0}), hash input:", DateTime.Now);
                    Console.WriteLine(string.Join(',', previousHashAndTransactions));
                    Console.WriteLine(newBlock.Nonce);
                    Console.WriteLine();
                    Console.WriteLine("Hash output: ");
                    Console.WriteLine(string.Join(',', blockByteArray));
                    Console.WriteLine();
                    Console.WriteLine("Whole block array:");
                    Console.WriteLine(string.Join(',', wholeBlock));
                    consoleSemaphore.Release();
                }
            }
            return newBlock;
        }

        private static byte[] GetPreviousHashAndTransactionByteArray(string previousBlockHash, List<Transaction> transactions)
        {
            var transactionsAsString = string.Join(",", transactions);
            byte[] encodedTransactions = Encoding.UTF8.GetBytes(transactionsAsString);
            byte[] previousBlockHashInBytes = Encoding.UTF8.GetBytes(previousBlockHash);
            return CombineByteArrays(previousBlockHashInBytes, encodedTransactions);
        }

        private (byte[], byte[]) HashNewBlock(byte[] previousHashAndTransactions, long nonce)
        {
            // this is broke
            byte[] encodedNonce = Encoding.UTF8.GetBytes(nonce.ToString());
            byte[] wholeBlock = CombineByteArrays(previousHashAndTransactions, encodedNonce);
            byte[] byteHash = sha256.ComputeHash(wholeBlock);
            return (byteHash, wholeBlock); //{ byteHash, wholeBlock }; // Convert.ToHexString(byteHash);
        }

        private static byte[] CombineByteArrays(byte[] first, byte[] second)
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
            if (Blocks.Count == 0) throw new Exception("The current chain is empty and a new block can therefore not be validated.");

            if (previousBlock.BlockHash.Equals(newBlock.PreviousBlockHash))
            {
                validateBlock = true;
                if (ValidateNewBlockHash(newBlock))
                {
                    return true;
                }
                Console.WriteLine("%%%%%%%%%%\nBlock hash not valid\n%%%%%%%%%%");
            }
            return false;
        }

        //private bool IsNodeAwareOfNewBlocksTransactions(PoWBlock newBlock)
        //{
        //    var intersection = RecievedTransactionsSinceLastBlock.Intersect(newBlock.Transactions);

        //    if (intersection.Count() == newBlock.Transactions.Count) { return true; }
        //    else
        //    {
        //        Console.WriteLine("%%%%%%%%%%\nNode unaware\n%%%%%%%%%%");
        //        return false;
        //    }
        //}

        private bool ValidateNewBlockHash(PoWBlock newBlock)
        {
            byte[] previousHashAndTransactions = GetPreviousHashAndTransactionByteArray(newBlock.PreviousBlockHash, newBlock.Transactions);

            var (blockByteArray, wholeBlock) = HashNewBlock(previousHashAndTransactions, newBlock.Nonce);
            string newBlocksHash = Convert.ToHexString(blockByteArray);
            consoleSemaphore.Wait();
            Console.WriteLine("Validate({0}): Block hash inputs:", DateTime.Now);
            Console.WriteLine(string.Join(',', previousHashAndTransactions));
            Console.WriteLine(newBlock.Nonce);
            Console.WriteLine();
            Console.WriteLine("Incoming block byte array:");
            Console.WriteLine(string.Join(',', blockByteArray));
            Console.WriteLine();
            Console.WriteLine("Whole block array:");
            Console.WriteLine(string.Join(',', wholeBlock));
            consoleSemaphore.Release();
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
