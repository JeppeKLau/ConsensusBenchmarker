﻿using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;
using ConsensusBenchmarker.Models.Blocks.ConsensusBlocks;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace ConsensusBenchmarker.Consensus.PoW
{
    public class PoWConsensus : ConsensusDriver
    {
        private readonly uint DifficultyLeadingZeroes = 7;
        private bool allowMining;
        private bool restartMining;
        private readonly Random random;
        private readonly SHA256 sha256;

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
                Console.WriteLine("Adding block:");
                allowMining = false;
                AddNewBlockToChain(block);
            }
            Console.WriteLine("Not adding block");
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
                string blockHash = HashNewBlock(previousHashAndTransactions, nonce);
                if (HashConformsToDifficulty(blockHash))
                {
                    newBlock = new PoWBlock(NodeID, DateTime.Now.ToLocalTime(), RecievedTransactionsSinceLastBlock.ToList(), blockHash, previousBlockHash, nonce);
                    AddNewBlockToChain(newBlock);
                }
            }
            return newBlock;
        }

        private static byte[] GetPreviousHashAndTransactionByteArray(string previousBlockHash, List<Transaction> transactions)
        {
            var transactionsAsString = string.Join(",", transactions.Select(x => x.ToString()));
            byte[] encodedTransactions = Encoding.UTF8.GetBytes(transactionsAsString);
            byte[] previousBlockHashInBytes = Encoding.UTF8.GetBytes(previousBlockHash);
            return CombineByteArrays(previousBlockHashInBytes, encodedTransactions);
        }

        private string HashNewBlock(byte[] previousHashAndTransactions, long nonce)
        {
            // this is broke
            byte[] encodedNonce = Encoding.UTF8.GetBytes(nonce.ToString());
            byte[] wholeBlock = CombineByteArrays(previousHashAndTransactions, encodedNonce);
            byte[] byteHash = sha256.ComputeHash(wholeBlock);
            return Convert.ToHexString(byteHash);
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

            Console.WriteLine($"Previous block hash: {previousBlock.BlockHash}");
            Console.WriteLine($"New blokc previous hash: {newBlock.PreviousBlockHash}");

            if (previousBlock.BlockHash.Equals(newBlock.PreviousBlockHash) && IsNodeAwareOfNewBlocksTransactions(newBlock))
            {
                if (ValidateNewBlockHash(newBlock))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsNodeAwareOfNewBlocksTransactions(PoWBlock newBlock)
        {
            var intersection = RecievedTransactionsSinceLastBlock.Intersect(newBlock.Transactions);

            if (intersection.Count() == newBlock.Transactions.Count) { return true; }
            else
            {
                return false;
            }
        }

        private bool ValidateNewBlockHash(PoWBlock newBlock)
        {
            byte[] previousHashAndTransactions = GetPreviousHashAndTransactionByteArray(newBlock.PreviousBlockHash, newBlock.Transactions);

            // broke

            string newBlocksHash = HashNewBlock(previousHashAndTransactions, newBlock.Nonce);
            Console.WriteLine("Validate: Block hash inputs:");
            previousHashAndTransactions.ToList().ForEach(x => Console.Write(x + ","));
            Console.WriteLine();
            Console.WriteLine(newBlock.Nonce);
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
