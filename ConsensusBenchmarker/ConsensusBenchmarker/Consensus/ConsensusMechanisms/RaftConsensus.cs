namespace ConsensusBenchmarker.Consensus.ConsensusMechanisms
{
    public class RaftConsensus : ConsensusDriver
    {
        public RaftConsensus(int nodeID, int maxBlocksToCreate) : base(nodeID, maxBlocksToCreate)
        {

        }
    }
}
