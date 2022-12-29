using ConsensusBenchmarker.Models.Blocks.ConsensusBlocks;
using ConsensusBenchmarker.Models.DTOs;
using ConsensusBenchmarker.Models.Events;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace ConsensusBenchmarker.Consensus.ConsensusMechanisms
{
    public enum RaftState
    {
        Follower = 0, Leader,
        Candidate
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
        private RaftState state = RaftState.Follower;

        private int term = 0; // Each time a leader is elected, increment it and broadcast it to the followers.
        private int nodesInNetwork = 0; // Can be decremented if a node is un-responsive.
        private int votesReceived = 0;
        private int commitIndex = 0; // Index of highest log entry known to be committed. Increases monotonically.
        private System.Timers.Timer electionTimeout;
        private int? votedFor = null;
        private readonly int maxElectionTimeout;

        public RaftConsensus(int nodeID, int maxBlocksToCreate, ref ConcurrentQueue<IEvent> eventQueue) : base(nodeID, maxBlocksToCreate, ref eventQueue)
        {
            nodesInNetwork = int.Parse(Environment.GetEnvironmentVariable("RAFT_NETWORKSIZE") ?? "0");
            maxElectionTimeout = int.Parse(Environment.GetEnvironmentVariable("RAFT_ELECTIONTIMEOUT") ?? "0.5") * 1000;
            electionTimeout = new();
            ResetElectionTimer();
        }

        private void ResetElectionTimer()
        {
            var newElectionTimeout = new System.Timers.Timer(new Random().Next(maxElectionTimeout / 2, maxElectionTimeout));
            newElectionTimeout.Elapsed += (sender, e) =>
            {
                StartElection();
            };
            newElectionTimeout.Start();
            electionTimeout = newElectionTimeout;
        }

        private void StartElection()
        {
            votesReceived = 1;
            votedFor = NodeID;
            term++;
            state = RaftState.Candidate;
            ResetElectionTimer();
            RequestVotes();
        }

        private void Elected()
        {
            state = RaftState.Leader;
            votesReceived = 0;
            votedFor = null;
            GetPreviousEntryInformation(out var previousLogIndex, out var previousElectionTerm);
            var heartBeatMessage = new RaftHeartbeat(term, NodeID, previousLogIndex, previousElectionTerm, null, commitIndex);
            eventQueue.Enqueue(new CommunicationEvent(heartBeatMessage, CommunicationEventType.RequestHeartBeat, null));
        }

        private void GetPreviousEntryInformation(out int previousLogIndex, out int previousElectionTerm)
        {
            blocksSemaphore.Wait();
            previousLogIndex = Blocks.FindLastIndex(0, Blocks.Count - 1, x => true);
            previousElectionTerm = (Blocks.ElementAt(previousLogIndex) as RaftBlock)?.ElectionTerm ?? throw new ArgumentException("Previous log entry is wrong block type");
            blocksSemaphore.Release();
        }

        public override void HandleVoteRequest(RaftVoteRequest voteRequest)
        {
            if (votedFor != null) return;
            int latestBlockIndex, latestBlockTerm;
            GetLatestEntryInformation(out latestBlockIndex, out latestBlockTerm);
            if (voteRequest.LatestBlockTerm >= latestBlockTerm)
            {
                if (voteRequest.LatestBlockIndex >= latestBlockIndex)
                {
                    eventQueue.Enqueue(new CommunicationEvent("true", CommunicationEventType.CastVote, voteRequest.NodeId));
                    votedFor = voteRequest.NodeId;
                    ResetElectionTimer();
                }
            }

            throw new NotImplementedException();
        }

        public override void HandleVoteReceived(bool vote)
        {
            votesReceived++;
            if (votesReceived > nodesInNetwork / 2)
            {
                Elected();
            }
            else
            {
                RequestVotes();
            }
        }

        public override void HandleHeartbeatRequest(RaftHeartbeat heartbeat) // Followers gets this
        {
            ResetElectionTimer();
            state = RaftState.Follower;
            var previousEntry = Blocks.ElementAtOrDefault(heartbeat.PreviousLogIndex) as RaftBlock;
            var success = previousEntry != null && previousEntry.ElectionTerm == heartbeat.PreviousLogTerm && term >= heartbeat.Term;
            var heartBeatResponse = new RaftHeartbeatResponse(term, success);
            eventQueue.Enqueue(new CommunicationEvent(heartBeatResponse, CommunicationEventType.ReceiveHeartBeat, heartbeat.LeaderId));
            if (!success && previousEntry != null)
            {
                Blocks.RemoveRange(heartbeat.PreviousLogIndex, Blocks.Count - heartbeat.PreviousLogIndex);
            }
            else if (heartbeat.Entries != null && !Blocks.Contains(heartbeat.Entries))
            {
                AddNewBlockToChain(heartbeat.Entries);
            }
            if (heartbeat.LeaderCommit > commitIndex)
            {
                GetLatestEntryInformation(out var latestEntryIndex, out _);
                commitIndex = Math.Min(heartbeat.LeaderCommit, latestEntryIndex);
            }
        }

        public override void HandleHeartbeatReceive(RaftHeartbeatResponse heartbeat) // Leaders gets this
        {

        }

        private void RequestVotes()
        {
            GetLatestEntryInformation(out var latestBlockIndex, out var latestBlockTerm);
            var voteRequest = new RaftVoteRequest(latestBlockIndex, latestBlockTerm, term, NodeID);
            eventQueue.Enqueue(new CommunicationEvent(JsonConvert.SerializeObject(voteRequest), CommunicationEventType.RequestVote, null));
        }

        private void GetLatestEntryInformation(out int latestBlockIndex, out int latestBlockTerm)
        {
            blocksSemaphore.Wait();
            latestBlockIndex = Blocks.FindLastIndex(x => true);
            latestBlockTerm = (Blocks.Last() as RaftBlock)?.ElectionTerm ?? throw new ArgumentException("Latest log entry is wrong block type");
            blocksSemaphore.Release();
        }

        private void StopConsensus()
        {
            electionTimeout.Stop();
            electionTimeout.Dispose();
        }
    }
}
