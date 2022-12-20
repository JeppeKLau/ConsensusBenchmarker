namespace ConsensusBenchmarker.Models.Blocks.ConsensusBlocks
{
    public class PoWBlock : Block
    {
        public PoWBlock(int ownerNodeID, DateTime blockCreatedAt, SortedList<(int, int), Transaction> transactions, string blockHash, string previousBlockHash, int nonce)
            : base(ownerNodeID, blockCreatedAt, transactions)
        {
            BlockHash = blockHash;
            PreviousBlockHash = previousBlockHash;
            Nonce = nonce;
        }

        public string BlockHash { get; set; }
        public string PreviousBlockHash { get; set; }
        public int Nonce { get; set; } = 0;

    }
}
