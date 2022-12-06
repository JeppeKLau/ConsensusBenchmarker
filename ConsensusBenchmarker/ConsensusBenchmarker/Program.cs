// See https://aka.ms/new-console-template for more information

using ConsensusBenchmarker.Communication;

namespace ConsensusBenchmarker;

class Program
{
    static async Task Main(string[] args)
    {
        string consensus = RetrieveConsensusMechanismType();

        CommunicationModule communicationModule = new CommunicationModule(consensus);
        await communicationModule.AnnounceOwnIP();
        // ask for blockchain
        await communicationModule.WaitForMessage();


        //var communicationTask = new Task(async () =>
        //{
        //    await communicationModule.WaitInstruction();
        //});

        //Parallel.Invoke(async () => await communicationModule.WaitInstruction());

        //Console.WriteLine("Task started");
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
