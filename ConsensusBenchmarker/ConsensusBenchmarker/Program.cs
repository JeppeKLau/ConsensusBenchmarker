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
        int nodeID = RetrieveNodeName();

        var dataCollectionModule = new DataCollectionModule();
        var communicationModule = new CommunicationModule(consensus, totalBlocksToCreate, nodeID);
        // ask for blockchain

        var communicationThread = new Thread(() =>
        {
            communicationModule.AnnounceOwnIP().GetAwaiter().GetResult();
            communicationModule.WaitForMessage().GetAwaiter().GetResult();
        });

        var dataCollectionThread = new Thread(() =>
        {
            dataCollectionModule.CollectData().GetAwaiter().GetResult();
        });

        dataCollectionThread.Start();
        communicationThread.Start();
        Console.WriteLine($"Communication thread is alive: {communicationThread.IsAlive}");

        while (communicationThread.IsAlive) ;
        Console.WriteLine("Communication thread is dead, terminating execution");
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
        throw new Exception("Could not parse the total block environment variable to an integer.");
    }

    private static int RetrieveNodeName()
    {
        var envString = Environment.GetEnvironmentVariable("REPLICA");
        if (envString == null)
        {
            throw new Exception("Could not get the replica environment variable.");
        }

        if (int.TryParse(envString, out int nodeID))
        {
            return nodeID;
        }
        throw new Exception("Could not parse the node id environment variable to an integer.");
    }
}
