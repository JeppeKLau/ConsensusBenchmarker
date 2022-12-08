using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks.ConsensusBlocks;
using System.Security.Cryptography;

namespace ConsensusBenchmarker.Consensus.PoW
{
    public class PoWConsensus : ConsensusDriver
    {
        private readonly uint DifficultyLeadingZeroes = 3;
        //private PoWBlock previousBlock;
        private readonly SHA256 sha256;

        public PoWConsensus(int nodeID)
        {
            this.nodeID = nodeID;
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


        // What happens if the node recieves a new transaction while "mining"?
        private PoWBlock CreateNewBlock(List<Transaction> transactions)
        {
            int nonce = 0;

            while (nonce != int.MaxValue)
            {




                nonce++;
            }


            return null;
        }

        private string HashNewBlock(PoWBlock previousBlock, List<Transaction> transactions, int nonce)
        {
            string transactionsAsString = string.Join(",", transactions.Select(x => x.ToString()));
            string wholeBlock = previousBlock.BlockHash + transactionsAsString + nonce.ToString();
            byte[] encodedBlock


            //byte[] hashValue = sha256.ComputeHash(fileStream);



            return "";
        }


    }
}
