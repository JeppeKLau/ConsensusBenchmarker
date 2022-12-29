namespace ConsensusBenchmarker.Models.DTOs
{
    public class RaftVoteRequest
    {
        public RaftVoteRequest(int latestBlockIndex, int latestBlockTerm, int electionTerm, int nodeId)
        {
            LatestBlockIndex = latestBlockIndex;
            LatestBlockTerm = latestBlockTerm;
            ElectionTerm = electionTerm;
            NodeId = nodeId;
        }

        public int LatestBlockIndex { get; set; }

        public int LatestBlockTerm { get; set; }

        public int ElectionTerm { get; set; }

        public int NodeId { get; set; }
    }
}
