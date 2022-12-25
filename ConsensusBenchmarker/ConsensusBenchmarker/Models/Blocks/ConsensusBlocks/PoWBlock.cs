namespace ConsensusBenchmarker.Models.Blocks.ConsensusBlocks
{
    public class PoWBlock : Block
    {
        public PoWBlock(int ownerNodeID, DateTime blockCreatedAt, List<Transaction> transactions, string blockHash, string previousBlockHash, long nonce)
            : base(BlockTypes.PoWBlock, ownerNodeID, blockCreatedAt, transactions)
        {
            BlockHash = blockHash;
            PreviousBlockHash = previousBlockHash;
            Nonce = nonce;
        }

        public string BlockHash { get; set; }
        public string PreviousBlockHash { get; set; }
        public long Nonce { get; set; } = 0;

        public override string ToString()
        {
            return base.ToString() +
                nameof(BlockHash) + " : " + BlockHash + "\n" +
                nameof(PreviousBlockHash) + " : " + PreviousBlockHash + "\n" +
                nameof(Nonce) + " : " + Nonce.ToString();
        }

    }
}
