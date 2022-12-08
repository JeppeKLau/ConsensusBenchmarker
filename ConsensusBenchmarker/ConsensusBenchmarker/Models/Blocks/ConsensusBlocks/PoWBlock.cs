using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsensusBenchmarker.Models.Blocks.ConsensusBlocks
{
    public class PoWBlock : Block
    {
        public PoWBlock(uint ownerNodeID, List<Transaction> transactions, string blockHash, string previousBlockHash, uint nonce) : base(ownerNodeID, transactions)
        {
            BlockHash = blockHash;
            PreviousBlockHash = previousBlockHash;
            Nonce = nonce;
        }
        
        public string BlockHash { get; set; } = string.Empty;
        public string PreviousBlockHash { get; set; } = string.Empty;
        public uint Nonce { get; set; } = 0;

    }
}
