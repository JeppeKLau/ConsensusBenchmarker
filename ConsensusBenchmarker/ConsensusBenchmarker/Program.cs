// See https://aka.ms/new-console-template for more information

using ConsensusBenchmarker.Communication;
using ConsensusBenchmarker.DataCollection;

namespace ConsensusBenchmarker;

class Program
{
    static async Task Main(string[] args)
    {
        string consensus = RetrieveConsensusMechanismType();
        var dataCollectionModule = new DataCollectionModule();
        var communicationModule = new CommunicationModule(consensus);
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
            throw new Exception("Could not fetch or recognize the environment variable.");
        }

        if (!ConsensusTypes.Contains(envString))
        {
            throw new Exception("Unknown consensus mechanism in environment variable");
        }
        return envString;
    }
}
