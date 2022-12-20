// See https://aka.ms/new-console-template for more information

using ConsensusBenchmarker.Communication;
using ConsensusBenchmarker.Consensus;
using ConsensusBenchmarker.DataCollection;
using ConsensusBenchmarker.Models.Events;

namespace ConsensusBenchmarker;

class Program
{
    static async Task Main()
    {
        string consensus = "PoW"; // RetrieveConsensusMechanismType();
        int totalBlocksToCreate = 100; // RetrieveNumberOfBlocksToCreate();
        int nodeID = 1; // RetrieveNodeName();

        var eventStack = new Stack<IEvent>();

        var dataCollectionModule = new DataCollectionModule(ref eventStack, nodeID);
        var communicationModule = new CommunicationModule(ref eventStack, nodeID);
        var consensusModule = new ConsensusModule(consensus, totalBlocksToCreate, nodeID, ref eventStack);
        await communicationModule.AnnounceOwnIP();
        // ask for blockchain ?

        var dataCollectionTask = dataCollectionModule.CollectData();
        var communicationTask = communicationModule.RunCommunication();
        var consensusTask = consensusModule.RunConsensus();

        await Task.WhenAll(communicationTask, dataCollectionTask, consensusTask);
        
        Console.WriteLine("All tasks are completed, terminating execution");
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
