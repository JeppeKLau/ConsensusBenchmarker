namespace ConsensusBenchmarker.Models.Blocks.ConsensusBlocks
{
    public class RaftBlock : Block
    {
        public RaftBlock(int electionTerm, int ownerNodeID, DateTime blockCreatedAt, List<Transaction> transactions) : base(BlockTypes.RaftBlock, ownerNodeID, blockCreatedAt, transactions)
        {
            ElectionTerm = electionTerm;
        }

        public int ElectionTerm { get; set; }
    }
}
