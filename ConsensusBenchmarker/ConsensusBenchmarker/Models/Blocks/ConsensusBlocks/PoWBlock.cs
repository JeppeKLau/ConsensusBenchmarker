namespace ConsensusBenchmarker.Models.Blocks.ConsensusBlocks
{
    public class PoWBlock : Block
    {
        public PoWBlock(int ownerNodeID, List<Transaction> transactions, string blockHash, string previousBlockHash, uint nonce) 
            : base(ownerNodeID, transactions)
        {
            BlockHash = blockHash;
            PreviousBlockHash = previousBlockHash;
            Nonce = nonce;
        }

        public string BlockHash { get; set; }
        public string PreviousBlockHash { get; set; }
        public uint Nonce { get; set; } = 0;

    }
}
