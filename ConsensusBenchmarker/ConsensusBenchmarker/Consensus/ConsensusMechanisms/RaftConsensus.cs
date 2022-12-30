using ConsensusBenchmarker.Models.Blocks.ConsensusBlocks;
using ConsensusBenchmarker.Models.DTOs;
using ConsensusBenchmarker.Models.Events;
using InfluxDB.Client.Api.Domain;
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
         * (X)  1. nextIndex array : describes the next log entry to send each server - initialized to leader last log index + 1; index should match with node ID
         *          - Updated when transmitting a new entry to a node.
         *      2. matchIndex array : describes the index of highest log entry known to be replicated on server - initialized to 0, increases monotonically; index should match with node ID
         *          - Used to find out which indecies should be committed. Updated when node returns success == true to indicate the entry was accepted
         *      3. Add committed Boolean to RaftBlock, marking if a block is committed. Should be updated by heartbeats to propagate commitment of blocks to follower nodes.
         *      4. lastApplied : index of the highest log entry applied to the state machine - initialized to 0, increases monotonically
         * (x)  5. Triple check the "Rules for servers" section of Figure 2 in paper (https://www.usenix.org/system/files/conference/atc14/atc14-paper-ongaro.pdf)
         * (X)  6. Consider refactor of RequestVote to more closely match the implementation described in Figure 2 in paper
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

        public override void HandleReceiveHeartBeat(RaftHeartbeatResponse heartbeatResponse) // Leaders gets this
        {
            if(currentTerm < heartbeatResponse.Term)
            {
                StepDownAsLeader(heartbeatResponse.Term);
            }
            if(state == RaftState.Leader && currentTerm == heartbeatResponse.Term)
            {
                if(heartbeatResponse.Success)
                {
                    
                }
                else
                {

                }
            }
        }

        public override void HandleReceiveVote(RaftVoteResponse voteResponse) // This method will be called FOREACH vote received
        {
            if (currentTerm < voteResponse.Term)
            {
                StepDownAsLeader(voteResponse.Term);
            }
            if (state == RaftState.Candidate && currentTerm == voteResponse.Term) 
            {
                totalVotesReceived++;
                if (voteResponse.VoteGranted) { votesForLeaderReceived++; }

                if (votesForLeaderReceived > Math.Floor((double)nodesInNetwork / 2))
                {
                    ElectNodeAsLeader();
                }
                else
                {
                    RequestVotes(); // This will be called A LOT, we should maybe put up a guard? // Should this even be here?
                }
            }
        }

        private void RequestVotes()
        {
            GetLatestEntryInformation(out var latestBlockIndex, out var latestBlockTerm);
            var voteRequest = new RaftVoteRequest(latestBlockIndex, latestBlockTerm, currentTerm, NodeID);
            eventQueue.Enqueue(new CommunicationEvent(voteRequest, CommunicationEventType.RequestVote, null));
        }

        private void StartElection()
        {
            InitializeCandidate();
            RequestVotes();
        }

        private void InitializeCandidate()
        {
            Console.WriteLine($"Node {NodeID} started an election and is now a leader candidate.");

            ResetElectionTimer();
            state = RaftState.Candidate;
            votedFor = NodeID;
            currentTerm++;
            nextIndex = 1;
            matchIndex = 0;
            votesForLeaderReceived = 1;
            totalVotesReceived = 1;
        }

        private void ElectNodeAsLeader()
        {
            GetPreviousEntryInformation(out var previousLogIndex, out int previousElectionTerm);
            InitializeLeader();

            // Request heartbeat:
            var heartbeatRequest = new RaftHeartbeatRequest(currentTerm, NodeID, previousLogIndex, previousElectionTerm, null, commitIndex);
            eventQueue.Enqueue(new CommunicationEvent(heartbeatRequest, CommunicationEventType.RequestHeartbeat, null));

            // We should maybe start a timer so that regular heartbeats can be sent out, so this node doesn't lose its leadership?.
        }

        private void InitializeLeader()
        {
            Console.WriteLine($"Node {NodeID} is now leader in term {currentTerm}.");

            ResetElectionTimer();
            state = RaftState.Leader;
            votesForLeaderReceived = 0;
            totalVotesReceived = 0;
            votedFor = null;

            nextIndex = BlocksInChain + 1;
            matchIndex = BlocksInChain;
        }

        private void GetPreviousEntryInformation(out int previousLogIndex, out int previousElectionTerm)
        {
            blocksSemaphore.Wait();  // I don't think its necessary to acquire the semaphores. There will only be one thread accessing this class at a time
            previousLogIndex = Blocks.FindLastIndex(0, Blocks.Count - 1, x => true);
            previousElectionTerm = (Blocks.ElementAt(previousLogIndex) as RaftBlock)?.ElectionTerm ?? throw new ArgumentException("Previous log entry is wrong block type");
            blocksSemaphore.Release();
        }

        #endregion

        public override void HandleRequestVote(RaftVoteRequest voteRequest) // Followers gets this
        {
            bool grantVote = false;
            if (currentTerm < voteRequest.ElectionTerm)
            {
                StepDownAsLeader(voteRequest.ElectionTerm);
            }
            if (votedFor == null)
            {
                int latestBlockIndex;
                GetLatestEntryInformation(out latestBlockIndex, out _);
                if (voteRequest.ElectionTerm >= currentTerm)
                {
                    if (voteRequest.LatestBlockIndex >= latestBlockIndex)
                    {
                        ResetElectionTimer();
                        grantVote = true;
                        votedFor = voteRequest.NodeId;
                    }
                }
            }
            eventQueue.Enqueue(new CommunicationEvent(new RaftVoteResponse(currentTerm, grantVote), CommunicationEventType.CastVote, voteRequest.NodeId));
        }

        public override void HandleRequestHeartBeat(RaftHeartbeatRequest heartbeat) // Followers gets this
        {
            bool success = false;
            if(currentTerm < heartbeat.Term)
            {
                StepDownAsLeader(heartbeat.Term);
            }
            if (heartbeat.Term >= currentTerm)
            {
                ResetElectionTimer();

                var previousEntry = Blocks.ElementAtOrDefault(heartbeat.PreviousLogIndex) as RaftBlock;
                if(previousEntry != null)
                {
                    if (previousEntry.ElectionTerm == heartbeat.PreviousLogTerm)
                    {
                        success = true;
                    }
                    else
                    {
                        Blocks.RemoveRange(heartbeat.PreviousLogIndex, Blocks.Count - heartbeat.PreviousLogIndex);
                    }
                }
                if (heartbeat.Entries != null && !Blocks.Any(x => x.Equals(heartbeat.Entries)))
                {
                    AddNewBlockToChain(heartbeat.Entries); // heartbeat.Entries is currently always null fyi
                }
                if (heartbeat.LeaderCommit > commitIndex)
                {
                    GetLatestEntryInformation(out var latestEntryIndex, out _);
                    commitIndex = Math.Min(heartbeat.LeaderCommit, latestEntryIndex);
                }
            }
            eventQueue.Enqueue(new CommunicationEvent(new RaftHeartbeatResponse(currentTerm, success), CommunicationEventType.ReceiveHeartbeat, heartbeat.LeaderId));


            // OLD:
            //var previousEntry = Blocks.ElementAtOrDefault(heartbeat.PreviousLogIndex) as RaftBlock;
            //var success = previousEntry != null && previousEntry.ElectionTerm == heartbeat.PreviousLogTerm && currentTerm >= heartbeat.Term;
            //var heartBeatResponse = new RaftHeartbeatResponse(currentTerm, success);
            //eventQueue.Enqueue(new CommunicationEvent(heartBeatResponse, CommunicationEventType.ReceiveHeartbeat, heartbeat.LeaderId));
            //if (!success && previousEntry != null)
            //{
            //    Blocks.RemoveRange(heartbeat.PreviousLogIndex, Blocks.Count - heartbeat.PreviousLogIndex);
            //}
            //else if (heartbeat.Entries != null && !Blocks.Any(x => x.Equals(heartbeat.Entries)))
            //{
            //    AddNewBlockToChain(heartbeat.Entries);
            //}
            //if (heartbeat.LeaderCommit > commitIndex)
            //{
            //    GetLatestEntryInformation(out var latestEntryIndex, out _);
            //    commitIndex = Math.Min(heartbeat.LeaderCommit, latestEntryIndex);
            //}
        }

        private void StepDownAsLeader(int term)
        {
            ResetElectionTimer();
            state = RaftState.Follower;
            currentTerm = term;
            votedFor = null;
            
            Console.WriteLine($"Node {NodeID} stepped down as leader.");
        }

        private void GetLatestEntryInformation(out int latestBlockIndex, out int latestBlockTerm)
        {
            blocksSemaphore.Wait(); // I don't think its necessary to acquire the semaphores. There will only be one thread accessing this class at a time
            latestBlockIndex = Blocks.FindLastIndex(x => true);
            latestBlockTerm = (Blocks.Last() as RaftBlock)?.ElectionTerm ?? throw new ArgumentException("Latest log entry is wrong block type");
            blocksSemaphore.Release();
        }
    }
}
