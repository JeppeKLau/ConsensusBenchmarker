// See https://aka.ms/new-console-template for more information

using ConsensusBenchmarker.Communication;
using ConsensusBenchmarker.Consensus;
using ConsensusBenchmarker.DataCollection;
using ConsensusBenchmarker.Models.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace ConsensusBenchmarker;

class Program
{
    static async Task Main(string[] _)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
        IServiceCollection serviceCollection = new ServiceCollection();
        var startup = new Startup(configuration);
        startup.ConfigureServices(serviceCollection);

        IServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

        JsonConvert.DefaultSettings = () => new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };

        var moduleThreads = new List<Thread>();
        string consensus = RetrieveConsensusMechanismType();
        int totalBlocksToCreate = RetrieveNumberOfBlocksToCreate();
        int nodeID = RetrieveNodeName();
        var executionFlag = true;

        var influxDBService = serviceProvider.GetRequiredService<InfluxDBService>();
        var eventQueue = new ConcurrentQueue<IEvent>();

        var dataCollectionModule = new DataCollectionModule(ref eventQueue, nodeID, influxDBService, ref executionFlag);
        var communicationModule = new CommunicationModule(ref eventQueue, nodeID);
        var consensusModule = new ConsensusModule(consensus, totalBlocksToCreate, nodeID, ref eventQueue);
        // ask for blockchain ?

        // Create threads:
        moduleThreads.AddRange(dataCollectionModule.SpawnThreads());
        moduleThreads.AddRange(communicationModule.SpawnThreads());
        moduleThreads.AddRange(consensusModule.SpawnThreads());

        // Start communication thread first, so nodes can discover each other before it begins:
        Console.WriteLine("Starting the communication thread.");
        moduleThreads[3].Start();
        await communicationModule.AnnounceOwnIP();
        Thread.Sleep(10_000);

        // Start threads:
        Console.WriteLine("Starting threads.");
        foreach (Thread moduleThread in moduleThreads)
        {
            moduleThread.Start();
        }

        // Wait for threads to finish:
        Console.WriteLine("Waiting for threads to finish.");
        foreach (Thread moduleThread in moduleThreads)
        {
            moduleThread.Join();
        }

        Console.WriteLine("Test complete, terminating execution.");
    }

    private static readonly string[] ConsensusTypes = { "PoW", "PoS", "PoC", "PoET", "Raft", "PBFT", "RapidChain" };

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
