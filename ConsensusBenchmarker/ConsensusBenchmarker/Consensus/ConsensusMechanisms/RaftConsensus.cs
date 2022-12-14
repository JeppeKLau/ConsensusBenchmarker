using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;
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
        // Persistent state of nodes:
        private RaftState state = RaftState.Follower;
        private readonly int nodesInNetwork;
        private int votesForLeaderReceived = 0;
        private int totalVotesReceived = 0;
        private int currentTerm = 0; // Each time a leader is elected, increment it
        private int? votedFor = null; // Holds which candidate this node voted for in each term (null if none)
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
                try
                {
                    if (AllowTimeout())
                    {
                        Console.WriteLine("Timout elapsed");
                        Console.WriteLine("Current elements in EventQueue: " + EventQueue.Count);
                        StartElection();
                    }
                    else
                    {
                        Console.WriteLine("Timeout was NOT allowed.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("StartElection threw: {0}", ex);
                }
            };
            electionTimeout.Start();
        }

        #region Election and Leader specific methods

        public override void HandleReceiveHeartBeat(RaftHeartbeatResponse heartbeatResponse)
        {
            if (currentTerm < heartbeatResponse.Term)
            {
                TransitionToFollower(heartbeatResponse.Term);
                return;
            }

            if (state == RaftState.Leader)
            {
                Console.WriteLine($"Received heartbeat response from node {heartbeatResponse.NodeId}, AddedEntry?: {heartbeatResponse.AddedEntry} success?: {heartbeatResponse.Success}.");
                var node = raftNodes.Single(x => x.NodeId == heartbeatResponse.NodeId);

                if (heartbeatResponse.Success)
                {
                    if (heartbeatResponse.Transaction is not null)
                    {
                        AddNewTransaction(heartbeatResponse.Transaction);
                    }

                    if (heartbeatResponse.AddedEntry is not null)
                    {
                        if (heartbeatResponse.AddedEntry == true)
                        {
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
                            if (node.NextIndex > 0) node.NextIndex--;
                            var preAppendEntry = (RaftBlock)Blocks.ElementAt(Math.Max(0, node.NextIndex - 1));
                            SendHeartBeat(new RaftHeartbeatRequest(currentTerm, NodeID, node.NextIndex - 1, preAppendEntry.ElectionTerm, null, commitIndex), node.NodeId);
                        }
                    }
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
            }
        }

        private void SendHeartBeat(RaftHeartbeatRequest heartbeat, int? recipient = null)
        {
            Console.WriteLine($"Node {NodeID} is sending Heartbeat requests in term {currentTerm}.");
            EventQueue.Enqueue(new CommunicationEvent(heartbeat, CommunicationEventType.RequestHeartbeat, recipient));
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
            GetPreviousEntryInformation(out var previousLogIndex, out int previousLogTerm);
            InitializeLeader();
            SendHeartBeat(new RaftHeartbeatRequest(currentTerm, NodeID, previousLogIndex, previousLogTerm, null, commitIndex));
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

        private void GetPreviousEntryInformation(out int previousLogIndex, out int previousLogTerm)
        {
            previousLogIndex = Math.Max(0, BlocksInChain - 2);
            previousLogTerm = (Blocks.ElementAtOrDefault(previousLogIndex) as RaftBlock)?.ElectionTerm ?? 0;
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
                Console.WriteLine("Current elements in EventQueue: " + EventQueue.Count);
                Console.WriteLine($"Transactions cache: {ReceivedTransactionsSinceLastBlock.Count}, total nodes in network (- 1): {nodesInNetwork - 1}");
                if (ReceivedTransactionsSinceLastBlock.Count == nodesInNetwork - 1)
                {
                    Console.WriteLine($"Leader: {NodeID} had an heartbeat timeout, and a new block will be created.");

                    var stopwatch = new Stopwatch();
                    GenerateNextTransaction(true);
                    EventQueue.Enqueue(new DataCollectionEvent(NodeID, DataCollectionEventType.IncTransaction, null));

                    stopwatch.Start();
                    RaftBlock newEntry = GenerateNextBlock(ref stopwatch);
                    AddNewBlockToChain(newEntry);
                    stopwatch.Stop();
                    EventQueue.Enqueue(new DataCollectionEvent(NodeID, DataCollectionEventType.IncBlock, stopwatch));

                    GetPreviousEntryInformation(out var previousLogIndex, out var previousElectionTerm);
                    SendHeartBeat(new RaftHeartbeatRequest(currentTerm, NodeID, previousLogIndex, previousElectionTerm, newEntry, commitIndex));
                    lastApplied++;
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

        public override void HandleRequestVote(RaftVoteRequest voteRequest)
        {
            bool grantVote = false;
            if (state != RaftState.Leader)
            {
                ResetElectionTimer();

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
            }
            Console.WriteLine($"Node {NodeID}, state: {state}, received a vote request from node {voteRequest.NodeId}. Grant vote?: {grantVote}.");
            EventQueue.Enqueue(new CommunicationEvent(new RaftVoteResponse(NodeID, currentTerm, grantVote), CommunicationEventType.CastVote, voteRequest.NodeId));
        }

        public override void HandleRequestHeartBeat(RaftHeartbeatRequest heartbeat)
        {
            ResetElectionTimer();

            if (state != RaftState.Follower || currentTerm < heartbeat.Term)
            {
                TransitionToFollower(heartbeat.Term);
            }

            if (heartbeat.Term < currentTerm || (BlocksInChain > 0 && Blocks.ElementAtOrDefault(heartbeat.PreviousLogIndex) is null))
            {
                Console.WriteLine($"Node {NodeID} received a heartbeat from node {heartbeat.LeaderId}. Success?: {false}.");
                Console.WriteLine("Failed due to {0}", heartbeat.Term < currentTerm ? "term mismatch" : "block missing");
                EventQueue.Enqueue(new CommunicationEvent(new RaftHeartbeatResponse(NodeID, currentTerm, false, false, null), CommunicationEventType.ReceiveHeartbeat, heartbeat.LeaderId));
                return;
            }

            bool? addedEntry = null;
            if (heartbeat.Entries is RaftBlock newEntry)
            {
                if (Blocks.Count > 0 && ((RaftBlock)Blocks.ElementAt(heartbeat.PreviousLogIndex)).ElectionTerm != newEntry.ElectionTerm)
                {
                    Blocks.RemoveRange(heartbeat.PreviousLogIndex + 1, 1);
                    addedEntry = false;
                }

                if (!Blocks.Any(x => x.Equals(heartbeat.Entries)))
                {
                    AddNewBlockToChain(newEntry);
                    EventQueue.Enqueue(new DataCollectionEvent(NodeID, DataCollectionEventType.IncBlock, null));
                    addedEntry = true;
                }

                if (heartbeat.LeaderCommit > commitIndex)
                {
                    commitIndex = Math.Min(heartbeat.LeaderCommit, Blocks.Count - 1);
                }
            }

            if(ExecutionFlag)
            {
                Transaction? newTransaction = null;
                Block? lastBlock = Blocks.LastOrDefault() ?? null;
                if (CreatedTransactionsByThisNode <= Blocks.Count || (addedEntry is not null && addedEntry == true) || (lastBlock is not null && lastBlock.OwnerNodeID != heartbeat.LeaderId))
                {
                    Console.WriteLine("Created new transaction for heartbeat response.");
                    newTransaction = GenerateNextTransaction();
                    EventQueue.Enqueue(new DataCollectionEvent(NodeID, DataCollectionEventType.IncTransaction, null));
                }
                if (newTransaction is not null || addedEntry is not null)
                {
                    Console.WriteLine($"Node {NodeID} responds to a heartbeat from node {heartbeat.LeaderId}. AddedEntry?: {addedEntry}. Success?: {true}.");
                    EventQueue.Enqueue(new CommunicationEvent(new RaftHeartbeatResponse(NodeID, currentTerm, addedEntry, true, newTransaction), CommunicationEventType.ReceiveHeartbeat, heartbeat.LeaderId));
                }
                else { Console.WriteLine($"Node {NodeID} received a heartbeat request, but did not, intentionally, respond."); }
                ResetElectionTimer();
            }
        }

        private void TransitionToFollower(int term)
        {
            Console.WriteLine($"Node {NodeID} was reset. Transistioned from {state} to {RaftState.Follower}.");
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

        private bool AllowTimeout()
        {
            if (state != RaftState.Leader)
            {
                return !EventQueue.Any(e => e is ConsensusEvent consensusEvent && (consensusEvent.EventType == ConsensusEventType.RequestHeartbeat || consensusEvent.EventType == ConsensusEventType.RequestVote));
            }
            return false;
        }
    }
}
