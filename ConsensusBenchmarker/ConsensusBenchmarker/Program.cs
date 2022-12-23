﻿// See https://aka.ms/new-console-template for more information

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

        var moduleThreads = new Dictionary<string, Thread>();
        string consensus = RetrieveConsensusMechanismType();
        int maxBlocksToCreate = RetrieveMaxBlocksToCreate();
        int nodeID = RetrieveNodeName();
        var startTime = DateTime.UtcNow;

        var influxDBService = serviceProvider.GetRequiredService<InfluxDBService>();
        var eventQueue = new ConcurrentQueue<IEvent>();

        var dataCollectionModule = new DataCollectionModule(ref eventQueue, nodeID, influxDBService, startTime);
        var communicationModule = new CommunicationModule(ref eventQueue, nodeID);
        var consensusModule = new ConsensusModule(consensus, maxBlocksToCreate, nodeID, ref eventQueue);
        // ask for blockchain ?

        // Create threads:
        Console.WriteLine($"Creating threads. Current threads in dictionary: {moduleThreads.Count()}");
        dataCollectionModule.SpawnThreads(moduleThreads);
        Console.WriteLine($"Created first batch of threads. Current threads in dictionary: {moduleThreads.Count()}");
        communicationModule.SpawnThreads(moduleThreads);
        Console.WriteLine($"Created second batch of threads. Current threads in dictionary: {moduleThreads.Count()}");
        consensusModule.SpawnThreads(moduleThreads);
        Console.WriteLine($"Created third batch of threads. Current threads in dictionary: {moduleThreads.Count()}");

        // For thread debugging:
        PrintActiveThreads(moduleThreads);

        // Start communication thread first, so nodes can discover each other before it begins:
        if (moduleThreads.TryGetValue("Communication_WaitForMessage", out var waitForMessageThread))
        {
            Console.WriteLine("Found and started communication_waitformsg thread.");
            waitForMessageThread.Start();
        }
        await communicationModule.AnnounceOwnIP();
        Thread.Sleep(10_000);

        // Start threads:
        foreach (KeyValuePair<string, Thread> moduleThread in moduleThreads.Where(t => t.Value.ThreadState == ThreadState.Unstarted))
        {
            moduleThread.Value.Start();
        }

        // Wait for threads to finish:
        Console.WriteLine("Waiting for test to finish.");
        foreach (KeyValuePair<string, Thread> moduleThread in moduleThreads)
        {
            moduleThread.Value.Join();
        }

        Console.WriteLine("Test complete, terminating execution.");
        Environment.Exit(0);
    }

    private static void PrintActiveThreads(Dictionary<string, Thread> moduleThreads)
    {
        var printActiveThreadsThread = new Thread(() =>
        {
            while(true)
            {
                int activeThreads = 0;
                foreach (KeyValuePair<string, Thread> moduleThread in moduleThreads)
                {
                    if(moduleThread.Value.ThreadState == ThreadState.Running)
                    {
                        activeThreads++;
                        Console.WriteLine($"Thread, {moduleThread.Key} is currently running.");
                    }
                }
                Console.WriteLine($"{activeThreads} active threads is currently running.\n\n");
                if (activeThreads == 0) break;
                Thread.Sleep(5_000);
            }
        });
        printActiveThreadsThread.Start();
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

    private static int RetrieveMaxBlocksToCreate()
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
