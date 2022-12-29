using ConsensusBenchmarker.Models.DTOs;
using ConsensusBenchmarker.Models.Events;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace ConsensusBenchmarker.Consensus.ConsensusMechanisms
{
    public enum RaftState { Follower = 0, Leader };

    public class RaftConsensus : ConsensusDriver
    {
        private RaftState state = RaftState.Follower;

        private int term = 0; // Each time a leader is elected, increment it and broadcast it to the followers.
        private int nodesInNetwork = 0; // Can be decremented if a node is un-responsive.
        private int votesReceived = 0;

        private System.Timers.Timer electionTimeout;
        private int? votedFor = null;
        private readonly int maxElectionTimeout;

        public RaftConsensus(int nodeID, int maxBlocksToCreate, ref ConcurrentQueue<IEvent> eventQueue) : base(nodeID, maxBlocksToCreate, ref eventQueue)
        {
            // 1. Elect leader
            //      - Know how many nodes are in cluster (network)
            //      - After an election timeout request votes
            //          - Only a client with up to date logs can become a leader
            // 2. Replicate leader's transactions
            //      - Takes "commands"/transactions from its clients, and appends them to its logs
            //      - Leader replicates its logs to the others


            nodesInNetwork = int.Parse(Environment.GetEnvironmentVariable("RAFT_NETWORKSIZE") ?? "0");
            maxElectionTimeout = int.Parse(Environment.GetEnvironmentVariable("RAFT_ELECTIONTIMEOUT") ?? "0.5") * 1000;
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
            this.electionTimeout = newElectionTimeout;
        }

        private void StartElection()
        {
            votesReceived = 0;
            term++;

            // Request votes
            // Upon receiving half the votes, declare yourself leader
            //      - Increment term
            //      - Send live RPC
            RequestVotes();
        }

        private void Elected()
        {

        }

        public override void HandleVoteRequest(RaftVoteRequest voteRequest)
        {
            if (votedFor != null) return;

            if (voteRequest.)

                if (voteRequest.TransactionList.Count >= BlocksInChain)
                {
                    eventQueue.Enqueue(new CommunicationEvent("true", CommunicationEventType.CastVote, voteRequest.NodeId));
                    votedFor = voteRequest.NodeId;
                }

            throw new NotImplementedException();
        }

        public override void HandleVoteReceived(bool vote)
        {
            votesReceived++;
            if (votesReceived >= nodesInNetwork / 2)
            {
                Elected();
            }
            else
            {
                RequestVotes();
            }
        }

        private void RequestVotes()
        {
            var voteRequest = new VoteRequest(Blocks, term, NodeID);
            eventQueue.Enqueue(new CommunicationEvent(JsonConvert.SerializeObject(voteRequest), CommunicationEventType.RequestVote, null));
        }

        private void StopConsensus()
        {
            electionTimeout.Stop();
            electionTimeout.Dispose();
        }
    }
}
