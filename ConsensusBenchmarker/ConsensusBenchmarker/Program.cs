// See https://aka.ms/new-console-template for more information

using ConsensusBenchmarker.Communication;
using ConsensusBenchmarker.DataCollection;

namespace ConsensusBenchmarker;

class Program
{
    static async Task Main(string[] args)
    {
        string consensus = RetrieveConsensusMechanismType();
        int totalBlocksToCreate = RetrieveNumberOfBlocksToCreate();

        var dataCollectionModule = new DataCollectionModule();
        var communicationModule = new CommunicationModule(consensus, totalBlocksToCreate);
        await communicationModule.AnnounceOwnIP();
        // ask for blockchain

        var communicationThread = new Thread(async () =>
        {
            await communicationModule.WaitForMessage();
        });

        var dataCollectionThread = new Thread(async () =>
        {
            await dataCollectionModule.CollectData();
        });

        dataCollectionThread.Start();
        communicationThread.Start();

        while (communicationThread.IsAlive) ;
    }

    private static string[] ConsensusTypes = { "PoW", "PoS", "PoC", "PoET", "Raft", "PBFT", "RapidChain" };

    private static string RetrieveConsensusMechanismType()
    {
        var envString = Environment.GetEnvironmentVariable("CONSENSUS_TYPE");
        if (envString == null)
        {
            throw new Exception("Could not get the consensus environment variable.");
        }

        if (!ConsensusTypes.Any(x => x.ToLower().Equals(envString.ToLower())))
        {
            throw new Exception("Unknown consensus mechanism in environment variable");
        }
        return ConsensusTypes.Single(x => x.ToLower().Equals(envString.ToLower()));
    }

    private static int RetrieveNumberOfBlocksToCreate()
    {
        var envString = Environment.GetEnvironmentVariable("TOTAL_BLOCKS");
        if (envString == null)
        {
            throw new Exception("Could not get the total block environment variable.");
        }

        if (int.TryParse(envString, out int numberOfBlocks))
        {
            return numberOfBlocks;
        }
        else
        {
            throw new Exception("Could not parse the total block environment variable to an integer.");
        }
    }
}
