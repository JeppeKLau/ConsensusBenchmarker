namespace ConsensusBenchmarker.Models.Blocks.ConsensusBlocks
{
    public class RaftBlock : Block
    {
        public RaftBlock(int ownerNodeID, DateTime blockCreatedAt, List<Transaction> transactions) : base(BlockTypes.RaftBlock, ownerNodeID, blockCreatedAt, transactions)
        {

        }

        public int ElectionTerm { get; set; }
    }
}
