using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks.ConsensusBlocks;
using System.Security.Cryptography;
using System.Text;

namespace ConsensusBenchmarker.Consensus.PoW
{
    public class PoWConsensus : ConsensusDriver
    {
        private readonly uint DifficultyLeadingZeroes = 5;
        private readonly SHA256 sha256;
        private PoWBlock? previousBlock;

        public PoWConsensus(int nodeID)
        {
            NodeID = nodeID;
            sha256 = SHA256.Create();
        }

        public override void RecieveBlock()
        {
            throw new NotImplementedException();
        }

        public override void RecieveTransaction(Transaction transaction)
        {
            throw new NotImplementedException();
        }

        #region MineNewBlock

        // What happens if the node recieves a new transaction while "mining"?
        private PoWBlock MineNewBlock(List<Transaction> transactions)
        {
            string previousBlockHash = "";
            PoWBlock? newBlock = null;
            uint nonce = 0;

            if (previousBlock != null)
            {
                previousBlockHash = previousBlock.BlockHash;
            }

            string transactionsAsString = string.Join(",", transactions.Select(x => x.ToString()));
            byte[] encodedTransactions = Encoding.UTF8.GetBytes(transactionsAsString);
            byte[] previousBlockHashInBytes = Encoding.UTF8.GetBytes(previousBlockHash);

            byte[] previousHashAndTransactions = CombineByteArrays(previousBlockHashInBytes, encodedTransactions);

            while (nonce != uint.MaxValue)
            {
                string blockHash = HashNewBlock(previousHashAndTransactions, nonce);
                if (HashConformsToDifficulty(blockHash))
                {
                    newBlock = new PoWBlock(NodeID, transactions, blockHash, previousBlockHash, nonce);
                    break;
                }
                nonce++;
            }

            if (newBlock == null)
            {
                throw new Exception("Could not mine a new block which conforms to the difficulty level.");
            }
            return newBlock;

        }

        private string HashNewBlock(byte[] previousHashAndTransactions, uint nonce)
        {
            byte[] encodedNonce = Encoding.UTF8.GetBytes(nonce.ToString());
            byte[] wholeBlock = CombineByteArrays(previousHashAndTransactions, encodedNonce);
            byte[] byteHash = sha256.ComputeHash(wholeBlock);
            return Convert.ToHexString(byteHash);
        }

        public byte[] CombineByteArrays(byte[] first, byte[] second)
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

    }
}
