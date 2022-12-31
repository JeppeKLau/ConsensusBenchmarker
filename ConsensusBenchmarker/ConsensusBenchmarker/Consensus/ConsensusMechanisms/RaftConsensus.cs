using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks.ConsensusBlocks;
using ConsensusBenchmarker.Models.DTOs;
using ConsensusBenchmarker.Models.Events;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ConsensusBenchmarker.Consensus.ConsensusMechanisms
{
    public enum RaftState
    {
        Follower = 0,
        Leader,
        Candidate,
    }

    class RaftNode
    {
        public RaftNode(int nodeId, int nextIndex, int matchIndex, bool? voteGranted)
        {
            NodeId = nodeId;
            NextIndex = nextIndex;
            MatchIndex = matchIndex;
            VoteGranted = voteGranted;
        }

        public int NodeId { get; set; }
        public int NextIndex { get; set; }
        public int MatchIndex { get; set; }
        public bool? VoteGranted { get; set; }
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
        private readonly int nodesInNetwork; // Can be decremented if a node is un-responsive??
        private int votesForLeaderReceived = 0;
        private int totalVotesReceived = 0;
        private int currentTerm = 0; // Each time a leader is elected, increment it
        private int? votedFor = null; // Holds which candidate this node voted for in each term (null if none)
        // ConsensusDriver's Blocks // Holds RaftBlocks a.k.a. the log entries.
        private readonly int maxElectionTimeout;
        private System.Timers.Timer? electionTimeout;
        private System.Timers.Timer? heartbeatTimeout;
        private readonly Random random;


        // Volatile state of nodes:
        private int commitIndex = 0; // Index of highest log entry known to be committed. Increases monotonically.
        private int lastApplied = 0; // index of highest log entry applied.


        // Volatile state of leader nodes: (Reinitialized after election)
        readonly List<RaftNode> raftNodes = new();


        public RaftConsensus(int nodeID, int maxBlocksToCreate, ConcurrentQueue<IEvent> eventQueue) : base(nodeID, maxBlocksToCreate, eventQueue)
        {
            nodesInNetwork = int.Parse(Environment.GetEnvironmentVariable("RAFT_NETWORKSIZE") ?? "0");
            maxElectionTimeout = (int)(double.Parse(Environment.GetEnvironmentVariable("RAFT_ELECTIONTIMEOUT") ?? "1") * 1000);
            random = new Random(NodeID * new Random().Next());
            ResetElectionTimer();
        }

        private void ResetElectionTimer()
        {
            electionTimeout?.Stop();
            electionTimeout = new(random.Next(maxElectionTimeout / 2, maxElectionTimeout)) { AutoReset = false };
            electionTimeout.Elapsed += (sender, e) =>
            {
                Console.WriteLine("Timout elapsed");
                try
                {
                    if (state != RaftState.Leader)
                    {
                        StartElection();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Election start threw: {0}", ex);
                }
            };
            electionTimeout.Start();
        }

        #region Election and Leader specific methods

        public override void HandleReceiveHeartBeat(RaftHeartbeatResponse heartbeatResponse) // Leaders gets this
        {
            if (currentTerm < heartbeatResponse.Term)
            {
                TransitionToFollower(heartbeatResponse.Term);
                return;
            }

            if (state == RaftState.Leader)
            {
                Console.WriteLine($"Received heartbeat response from node {heartbeatResponse.NodeId}, it's request from the leader was {heartbeatResponse.Success}.");
                var node = raftNodes.Single(x => x.NodeId == heartbeatResponse.NodeId);

                if (heartbeatResponse.Success)
                {
                    Console.WriteLine($"Adding new transaction from node {node.NodeId}.");
                    if (heartbeatResponse.Transaction is not null) AddNewTransaction(heartbeatResponse.Transaction);
                    node.NextIndex++;
                    node.MatchIndex++;
                    if (node.NextIndex < lastApplied)
                    {
                        Console.WriteLine($"Sending new entry to node {node.NodeId}");
                        var appendEntry = (RaftBlock)Blocks.ElementAt(node.NextIndex);
                        var preAppendEntry = (RaftBlock)Blocks.ElementAt(node.NextIndex - 1);
                        SendHeartBeat(new RaftHeartbeatRequest(currentTerm, NodeID, node.NextIndex - 1, preAppendEntry.ElectionTerm, appendEntry, commitIndex), node.NodeId);
                    }
                }
                else
                {
                    Console.WriteLine("Heartbeat response was false.");
                    if (node.NextIndex > 0) node.NextIndex--;
                    var preAppendEntry = (RaftBlock)Blocks.ElementAt(Math.Max(0, node.NextIndex - 1));
                    SendHeartBeat(new RaftHeartbeatRequest(currentTerm, NodeID, node.NextIndex - 1, preAppendEntry.ElectionTerm, null, commitIndex), node.NodeId);
                }
            }
        }

        public override RaftBlock GenerateNextBlock(ref Stopwatch Stopwatch)
        {
            var newEntry = new RaftBlock(currentTerm, NodeID, DateTime.UtcNow, ReceivedTransactionsSinceLastBlock.ToList());
            ReceivedTransactionsSinceLastBlock.Clear();
            return newEntry;
        }

        public override void HandleReceiveVote(RaftVoteResponse voteResponse)
        {
            ResetElectionTimer();
            if (currentTerm < voteResponse.Term)
            {
                TransitionToFollower(voteResponse.Term);
            }

            Console.WriteLine($"Received a vote. Node {NodeID} term is: {currentTerm} and node {voteResponse.NodeId}'s term is {voteResponse.Term}");

            totalVotesReceived++;
            if (state == RaftState.Candidate && currentTerm == voteResponse.Term)
            {
                if (voteResponse.VoteGranted) { votesForLeaderReceived++; }
                raftNodes.Single(x => x.NodeId == voteResponse.NodeId).VoteGranted = voteResponse.VoteGranted;

                Console.WriteLine($"Node {NodeID} received a vote from node {voteResponse.NodeId}, the vote was: {voteResponse.VoteGranted}.");
                Console.WriteLine($"The total votes received is: {totalVotesReceived}, the votes in favor of this node is: {votesForLeaderReceived}");

                if (votesForLeaderReceived > nodesInNetwork / 2)
                {
                    ElectNodeAsLeader();
                }
                else
                {
                    Thread.Sleep(1);
                    if (EventQueue.Where(e => e is ConsensusEvent consensusEvent && consensusEvent.EventType == ConsensusEventType.ReceiveVote).ToList().Count <= 1)
                    {
                        foreach (RaftNode node in raftNodes)
                        {
                            if (node.VoteGranted != true)
                            {
                                Console.WriteLine($"Node {NodeID} Re-Requested votes from {node.NodeId}");
                                RequestVotes(node.NodeId);
                            }
                        }
                    }
                }
            }
        }

        private void SendHeartBeat(RaftHeartbeatRequest heartbeat, int? recipient = null)
        {
            EventQueue.Enqueue(new CommunicationEvent(heartbeat, CommunicationEventType.RequestHeartbeat, recipient));
            Console.WriteLine($"Node {NodeID} is sending Heartbeat requests in term {currentTerm}.");
        }

        private void RequestVotes(int? nodeId = null)
        {
            GetLatestEntryInformation(out var latestBlockIndex, out var latestBlockTerm);
            var voteRequest = new RaftVoteRequest(latestBlockIndex, latestBlockTerm, currentTerm, NodeID);
            EventQueue.Enqueue(new CommunicationEvent(voteRequest, CommunicationEventType.RequestVote, nodeId));
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
            votesForLeaderReceived = 1;
            totalVotesReceived = 1;
            heartbeatTimeout?.Dispose();

            InitializeRaftNodeList(1, 0);
            raftNodes.Single(x => x.NodeId == NodeID).VoteGranted = true;
        }

        private void ElectNodeAsLeader()
        {
            GetPreviousEntryInformation(out var previousLogIndex, out int previousElectionTerm);
            InitializeLeader();

            var heartbeatRequest = new RaftHeartbeatRequest(currentTerm, NodeID, previousLogIndex, previousElectionTerm, null, commitIndex);
            SendHeartBeat(heartbeatRequest);
        }

        private void InitializeLeader()
        {
            Console.WriteLine($"Node {NodeID} is now leader in term {currentTerm}.");

            electionTimeout!.Stop();
            state = RaftState.Leader;
            votesForLeaderReceived = 0;
            totalVotesReceived = 0;
            votedFor = null;

            heartbeatTimeout = new System.Timers.Timer(maxElectionTimeout / 4) { AutoReset = true };
            heartbeatTimeout.Elapsed += (sender, e) =>
            {
                AppendNewBlock();
            };
            heartbeatTimeout.Start();

            InitializeRaftNodeList(BlocksInChain, 0);
        }

        private void GetPreviousEntryInformation(out int previousLogIndex, out int previousElectionTerm)
        {
            previousLogIndex = Math.Max(0, BlocksInChain - 2);
            previousElectionTerm = (Blocks.ElementAtOrDefault(previousLogIndex) as RaftBlock)?.ElectionTerm ?? 0;
        }

        private void InitializeRaftNodeList(int nextIndex, int matchIndex)
        {
            raftNodes.Clear();
            for (int index = 1; index <= nodesInNetwork; index++)
            {
                raftNodes.Add(new RaftNode(index, nextIndex, matchIndex, null));
            }
        }

        private void AppendNewBlock()
        {
            if (ExecutionFlag)
            {
                if (ReceivedTransactionsSinceLastBlock.Count > nodesInNetwork / 2)
                {
                    Console.WriteLine($"Leader: {NodeID} had an heartbeat timeout, and a new block will be created.");

                    var stopwatch = new Stopwatch();
                    GenerateNextTransaction(true);
                    stopwatch.Start();
                    RaftBlock newEntry = GenerateNextBlock(ref stopwatch);
                    AddNewBlockToChain(newEntry);
                    stopwatch.Stop();
                    GetPreviousEntryInformation(out var previousLogIndex, out var previousElectionTerm);

                    SendHeartBeat(new RaftHeartbeatRequest(currentTerm, NodeID, previousLogIndex, previousElectionTerm, newEntry, commitIndex));
                    lastApplied++;

                    Console.WriteLine($"Node {NodeID} is leader and has created a new block at {newEntry.BlockCreatedAt:T} in term: {newEntry.ElectionTerm}.");
                }
                else
                {
                    Console.WriteLine($"Leader: {NodeID} had an heartbeat timeout, but a new block could not be created.");
                    GetPreviousEntryInformation(out var previousLogIndex, out var previousElectionTerm);
                    SendHeartBeat(new RaftHeartbeatRequest(currentTerm, NodeID, previousLogIndex, previousElectionTerm, null, commitIndex));
                }

                if (ExecutionFlag == false)
                {
                    electionTimeout!.Dispose();
                    heartbeatTimeout?.Dispose();
                }
            }
        }

        #endregion

        public override void HandleRequestVote(RaftVoteRequest voteRequest) // Followers gets this
        {
            ResetElectionTimer();

            bool grantVote = false;
            if (currentTerm < voteRequest.ElectionTerm)
            {
                TransitionToFollower(voteRequest.ElectionTerm);
            }
            if (votedFor == null)
            {
                GetLatestEntryInformation(out int latestBlockIndex, out _);
                if (voteRequest.ElectionTerm >= currentTerm)
                {
                    if (voteRequest.LatestBlockIndex >= latestBlockIndex)
                    {
                        grantVote = true;
                        votedFor = voteRequest.NodeId;
                    }
                }
            }
            Console.WriteLine($"Node {NodeID} received a vote request from node {voteRequest.NodeId}. Grant vote?: {grantVote}.");
            EventQueue.Enqueue(new CommunicationEvent(new RaftVoteResponse(NodeID, currentTerm, grantVote), CommunicationEventType.CastVote, voteRequest.NodeId));
        }

        public override void HandleRequestHeartBeat(RaftHeartbeatRequest heartbeat)
        {
            ResetElectionTimer();

            Transaction? newTransaction = null;

            if (state == RaftState.Candidate)
            {
                TransitionToFollower(heartbeat.Term);
            }

            if (heartbeat.Term < currentTerm || (BlocksInChain > 0 && Blocks.ElementAtOrDefault(heartbeat.PreviousLogIndex) is null))
            {
                Console.WriteLine($"Node {NodeID} received a heartbeat from node {heartbeat.LeaderId}. Success?: {false}.");
                Console.WriteLine("Failed due to {0}", heartbeat.Term < currentTerm ? "term mismatch" : "block missing");
                EventQueue.Enqueue(new CommunicationEvent(new RaftHeartbeatResponse(NodeID, currentTerm, false, newTransaction), CommunicationEventType.ReceiveHeartbeat, heartbeat.LeaderId));
                return;
            }

            if (heartbeat.Entries is RaftBlock newEntry)
            {
                if (((RaftBlock)Blocks.ElementAt(heartbeat.PreviousLogTerm + 1)).ElectionTerm != newEntry.ElectionTerm)
                {
                    Blocks.RemoveRange(heartbeat.PreviousLogIndex + 1, 1);
                }

                if (!Blocks.Contains(newEntry))
                {
                    AddNewBlockToChain(newEntry);
                    newTransaction = GenerateNextTransaction();
                }

                if (heartbeat.LeaderCommit > commitIndex)
                {
                    commitIndex = Math.Min(heartbeat.LeaderCommit, BlocksInChain - 1);
                }
            }

            Console.WriteLine($"Node {NodeID} received a heartbeat from node {heartbeat.LeaderId}. Success?: {true}.");
            EventQueue.Enqueue(new CommunicationEvent(new RaftHeartbeatResponse(NodeID, currentTerm, true, newTransaction), CommunicationEventType.ReceiveHeartbeat, heartbeat.LeaderId));




            //if (currentTerm < heartbeat.Term)
            //{
            //    TransitionToFollower(heartbeat.Term);
            //}
            //if (heartbeat.Term >= currentTerm)
            //{
            //    GetPreviousEntryInformation(out var previousLogIndex, out var previousLogTerm);
            //    if (previousLogTerm == heartbeat.PreviousLogTerm)
            //    {
            //        success = true;
            //        newTransaction = GenerateNextTransaction();
            //    }
            //    else if (previousLogIndex == heartbeat.PreviousLogIndex)
            //    {
            //        if (BlocksInChain > 0)
            //        {
            //            Console.WriteLine($"Removing index {heartbeat.PreviousLogIndex} with {BlocksInChain} blocks in chain");
            //            Blocks.RemoveRange(heartbeat.PreviousLogIndex, Math.Max(1, BlocksInChain - heartbeat.PreviousLogIndex));
            //        }
            //    }
            //    if (heartbeat.Entries != null && !Blocks.Any(x => x.Equals(heartbeat.Entries)))
            //    {
            //        AddNewBlockToChain(heartbeat.Entries);
            //    }
            //    if (heartbeat.LeaderCommit > commitIndex)
            //    {
            //        GetLatestEntryInformation(out var latestEntryIndex, out _);
            //        commitIndex = Math.Min(heartbeat.LeaderCommit, latestEntryIndex);
            //    }
            //}
            //Console.WriteLine($"Node {NodeID} received a heartbeat from node {heartbeat.LeaderId}. Success?: {success}.");
            //EventQueue.Enqueue(new CommunicationEvent(new RaftHeartbeatResponse(NodeID, currentTerm, success, newTransaction), CommunicationEventType.ReceiveHeartbeat, heartbeat.LeaderId));
        }

        private void TransitionToFollower(int term)
        {
            ResetElectionTimer();

            Console.WriteLine($"Node {NodeID} transistioned from {state} to {RaftState.Follower}.");
            state = RaftState.Follower;
            currentTerm = term;
            votedFor = null;

            heartbeatTimeout?.Dispose();
        }

        private void GetLatestEntryInformation(out int latestBlockIndex, out int latestBlockTerm)
        {
            latestBlockIndex = Math.Max(0, BlocksInChain - 1);
            latestBlockTerm = (Blocks.ElementAtOrDefault(latestBlockIndex) as RaftBlock)?.ElectionTerm ?? 0;
        }
    }
}
