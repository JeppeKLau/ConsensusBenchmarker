using ConsensusBenchmarker.Models;

namespace ConsensusBenchmarker.Consensus
{
    public interface IConsensus
    {
        void RecieveTransaction(Transaction transaction);

        void RecieveBlock();



    }
}
