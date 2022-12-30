using ConsensusBenchmarker.Models.Blocks.ConsensusBlocks;
using ConsensusBenchmarker.Models.DTOs;
using ConsensusBenchmarker.Models.Events;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ConsensusBenchmarker.Consensus.ConsensusMechanisms
{
    public enum RaftState
    {
        Follower = 0, 
        Leader,
        Candidate,
    }

    public class RaftConsensus : ConsensusDriver
    {
        /*
         * Raft TODO list:
         *      1. nextIndex array : describes the next log entry to send each server - initialized to leader last log index + 1; index should match with node ID
         *          - Updated when transmitting a new entry to a node.
         *      2. matchIndex array : describes the index of highest log entry known to be replicated on server - initialized to 0, increases monotonically; index should match with node ID
         *          - Used to find out which indecies should be committed. Updated when node returns success == true to indicate the entry was accepted
         *      3. Add committed Boolean to RaftBlock, marking if a block is committed. Should be updated by heartbeats to propagate commitment of blocks to follower nodes.
         *      4. lastApplied : index of the highest log entry applied to the state machine - initialized to 0, increases monotonically
         *      5. Triple check the "Rules for servers" section of Figure 2 in paper (https://www.usenix.org/system/files/conference/atc14/atc14-paper-ongaro.pdf)
         *      6. Consider refactor of RequestVote to more closely match the implementation described in Figure 2 in paper
         */


        // Persistent state of nodes:
        private RaftState state = RaftState.Follower;
        private int nodesInNetwork = 0; // Can be decremented if a node is un-responsive??
        private int votesForLeaderReceived = 0;
        private int totalVotesReceived = 0;
        private int currentTerm = 0; // Each time a leader is elected, increment it
        private int? votedFor = null; // Holds which candidate this node voted for in each term (null if none)
        // ConsensusDriver's Blocks // Holds RaftBlocks a.k.a. the log entries.
        private readonly int maxElectionTimeout;
        private System.Timers.Timer electionTimeout;


        // Volatile state of nodes:
        private int commitIndex = 0; // Index of highest log entry known to be committed. Increases monotonically.
        private int lastApplied = 0; // index of highest log entry applied.


        // Volatile state of leader nodes: (Reinitialized after election)
        private int nextIndex = 0; //  index of the next log entry to send initialized to leader last log index + 1
        private int matchIndex = 0; // index of highest log entry known to be replicated, (initialized to 0, increases monotonically).


        public RaftConsensus(int nodeID, int maxBlocksToCreate, ref ConcurrentQueue<IEvent> eventQueue) : base(nodeID, maxBlocksToCreate, ref eventQueue)
        {
            nodesInNetwork = int.Parse(Environment.GetEnvironmentVariable("RAFT_NETWORKSIZE") ?? "0");
            maxElectionTimeout = int.Parse(Environment.GetEnvironmentVariable("RAFT_ELECTIONTIMEOUT") ?? "0.5") * 1000;
            electionTimeout = new();
            electionTimeout.AutoReset = true;
            electionTimeout.Elapsed += (sender, e) =>
            {
                StartElection();
            };
            ResetElectionTimer();
        }

        private void ResetElectionTimer()
        {
            electionTimeout.Stop();
            electionTimeout = new System.Timers.Timer(new Random(NodeID).Next(maxElectionTimeout / 2, maxElectionTimeout));
            electionTimeout.Start();
        }

        private void StopConsensus()
        {
            electionTimeout.Stop();
            electionTimeout.Dispose();
        }

        #region Election and Leader specific methods

        public override void HandleReceiveHeartBeat(RaftHeartbeatResponse heartbeat) // Leaders gets this
        {

        }

        public override void HandleReceiveVote(bool vote) // This method will be called FOREACH vote received
        {
            totalVotesReceived++;
            if (vote) { votesForLeaderReceived++; }

            if (state == RaftState.Candidate)
            {
                if (votesForLeaderReceived > Math.Floor((double)nodesInNetwork / 2))
                {
                    ElectNodeAsLeader();
                }
                else if(votesForLeaderReceived != totalVotesReceived)
                {
                    RequestVotes(); // This will be called a lot, we should maybe put up a guard? // I tried something^
                }
            }
        }

        private void RequestVotes()
        {
            GetLatestEntryInformation(out var latestBlockIndex, out var latestBlockTerm);
            var voteRequest = new RaftVoteRequest(latestBlockIndex, latestBlockTerm, currentTerm, NodeID);
            eventQueue.Enqueue(new CommunicationEvent(JsonConvert.SerializeObject(voteRequest), CommunicationEventType.RequestVote, null));
        }

        private void StartElection()
        {
            ResetElectionTimer();
            state = RaftState.Candidate;
            votesForLeaderReceived = 1;
            votedFor = NodeID;
            currentTerm++;
            RequestVotes();
        }

        private void ElectNodeAsLeader()
        {
            Console.WriteLine($"Node {NodeID} is leader in term {currentTerm}.");

            GetPreviousEntryInformation(out var previousLogIndex, out int previousElectionTerm);
            InitializeLeader(previousLogIndex);

            // Request heartbeat:
            var heartBeatMessage = new RaftHeartbeat(currentTerm, NodeID, previousLogIndex, previousElectionTerm, null, commitIndex);
            eventQueue.Enqueue(new CommunicationEvent(heartBeatMessage, CommunicationEventType.RequestHeartBeat, null));
        }

        private void InitializeLeader(int previousLogIndex)
        {
            ResetElectionTimer();
            state = RaftState.Leader;
            votesForLeaderReceived = 0;
            votedFor = null;

            nextIndex = previousLogIndex + 1;
            matchIndex = previousLogIndex;
        }

        private void GetPreviousEntryInformation(out int previousLogIndex, out int previousElectionTerm)
        {
            blocksSemaphore.Wait();
            previousLogIndex = Blocks.FindLastIndex(0, Blocks.Count - 1, x => true);
            previousElectionTerm = (Blocks.ElementAt(previousLogIndex) as RaftBlock)?.ElectionTerm ?? throw new ArgumentException("Previous log entry is wrong block type");
            blocksSemaphore.Release();
        }

        #endregion

        public override void HandleRequestVote(RaftVoteRequest voteRequest) // Followers gets this
        {
            if(currentTerm < voteRequest.ElectionTerm)
            {

            }

            if (votedFor == null)
            {
                int latestBlockIndex, latestBlockTerm;
                GetLatestEntryInformation(out latestBlockIndex, out latestBlockTerm);
                if (voteRequest.LatestBlockTerm >= latestBlockTerm)
                {
                    if (voteRequest.LatestBlockIndex >= latestBlockIndex)
                    {
                        eventQueue.Enqueue(new CommunicationEvent("true", CommunicationEventType.CastVote, voteRequest.NodeId));
                        votedFor = voteRequest.NodeId;
                        ResetElectionTimer();
                        return;
                    }
                }
                eventQueue.Enqueue(new CommunicationEvent("false", CommunicationEventType.CastVote, voteRequest.NodeId));
            }
        }

        public override void HandleRequestHeartBeat(RaftHeartbeat heartbeat) // Followers gets this
        {
            StepDownAsLeader(heartbeat);

            var previousEntry = Blocks.ElementAtOrDefault(heartbeat.PreviousLogIndex) as RaftBlock;
            var success = previousEntry != null && previousEntry.ElectionTerm == heartbeat.PreviousLogTerm && currentTerm >= heartbeat.Term;
            var heartBeatResponse = new RaftHeartbeatResponse(currentTerm, success);
            eventQueue.Enqueue(new CommunicationEvent(heartBeatResponse, CommunicationEventType.ReceiveHeartBeat, heartbeat.LeaderId));
            if (!success && previousEntry != null)
            {
                Blocks.RemoveRange(heartbeat.PreviousLogIndex, Blocks.Count - heartbeat.PreviousLogIndex);
            }
            else if (heartbeat.Entries != null && !Blocks.Any(x => x.Equals(heartbeat.Entries)))
            {
                AddNewBlockToChain(heartbeat.Entries);
            }
            if (heartbeat.LeaderCommit > commitIndex)
            {
                GetLatestEntryInformation(out var latestEntryIndex, out _);
                commitIndex = Math.Min(heartbeat.LeaderCommit, latestEntryIndex);
            }
            // something should be sent back right? RaftheartbeatResponse?
        }

        private void StepDownAsLeader(RaftHeartbeat heartbeat)
        {
            ResetElectionTimer();
            state = RaftState.Follower;
            currentTerm = heartbeat.Term;
            votedFor = null;
            
            Console.WriteLine($"Node {NodeID} is follower in term {heartbeat.Term}.");
        }

        private void GetLatestEntryInformation(out int latestBlockIndex, out int latestBlockTerm)
        {
            blocksSemaphore.Wait();
            latestBlockIndex = Blocks.FindLastIndex(x => true);
            latestBlockTerm = (Blocks.Last() as RaftBlock)?.ElectionTerm ?? throw new ArgumentException("Latest log entry is wrong block type");
            blocksSemaphore.Release();
        }
    }
}
