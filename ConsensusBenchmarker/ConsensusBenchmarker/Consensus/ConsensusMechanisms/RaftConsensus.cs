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
        private int nodesInNetwork = 0; // Can be decremented if a node is un-responsive??
        private int votesForLeaderReceived = 0;
        private int totalVotesReceived = 0;
        private int currentTerm = 0; // Each time a leader is elected, increment it
        private int? votedFor = null; // Holds which candidate this node voted for in each term (null if none)
        // ConsensusDriver's Blocks // Holds RaftBlocks a.k.a. the log entries.
        private readonly int maxElectionTimeout;
        private System.Timers.Timer electionTimeout;
        private readonly Random random;


        // Volatile state of nodes:
        private int commitIndex = 0; // Index of highest log entry known to be committed. Increases monotonically.
        private int lastApplied = 0; // index of highest log entry applied.


        // Volatile state of leader nodes: (Reinitialized after election)
        List<RaftNode> raftNodes = new();


        public RaftConsensus(int nodeID, int maxBlocksToCreate, ref ConcurrentQueue<IEvent> eventQueue) : base(nodeID, maxBlocksToCreate, ref eventQueue)
        {
            nodesInNetwork = int.Parse(Environment.GetEnvironmentVariable("RAFT_NETWORKSIZE") ?? "0");
            maxElectionTimeout = int.Parse(Environment.GetEnvironmentVariable("RAFT_ELECTIONTIMEOUT") ?? "0.5") * 1000;
            random = new Random(NodeID * new Random().Next());
            electionTimeout = new();
            electionTimeout.AutoReset = false;
            electionTimeout.Elapsed += (sender, e) =>
            {
                StartElection();
            };
            ResetElectionTimer();
        }

        private void ResetElectionTimer()
        {
            electionTimeout.Interval = random.Next(maxElectionTimeout / 2, maxElectionTimeout);
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
                var appendEntries = false;
                var node = raftNodes.Single(x => x.NodeId == heartbeatResponse.NodeId);
                if (heartbeatResponse.Success)
                {
                    AddNewTransaction(heartbeatResponse.Transaction!);
                    node.NextIndex++;
                    node.MatchIndex++;
                    if (node.NextIndex >= lastApplied) appendEntries = true;
                }
                else
                {
                    node.NextIndex--;
                    appendEntries = true;
                }
                if (appendEntries)
                {
                    var appendEntry = (RaftBlock)Blocks.ElementAt(node.NextIndex);
                    var preAppendEntry = (RaftBlock)Blocks.ElementAt(node.NextIndex - 1);
                    SendHeartBeat(new RaftHeartbeatRequest(currentTerm, NodeID, node.NextIndex - 1, preAppendEntry.ElectionTerm, appendEntry, commitIndex))
                }
            }
        }

        public override void AddNewTransaction(Transaction transaction)
        {
            base.AddNewTransaction(transaction);
            var stopwatch = new Stopwatch();
            if (ReceivedTransactionsSinceLastBlock.Count >= nodesInNetwork / 2)
            {
                GenerateNextTransaction(true);
                stopwatch.Start();
                RaftBlock newEntry = GenerateNextBlock(ref stopwatch);
                AddNewBlockToChain(newEntry);
                stopwatch.Stop();
                GetPreviousEntryInformation(out var previousLogIndex, out var previousElectionTerm);
                SendHeartBeat(new RaftHeartbeatRequest(currentTerm, NodeID, previousLogIndex, previousElectionTerm, newEntry, commitIndex));
                lastApplied++;

                Console.WriteLine($"Node {NodeID} is leader and has created a new block at {newEntry.BlockCreatedAt:T} in term: {newEntry.ElectionTerm}.");

                if (ExecutionFlag == false)
                {
                    electionTimeout.Dispose();
                }
            }
        }

        public override RaftBlock GenerateNextBlock(ref Stopwatch Stopwatch)
        {
            var newEntry = new RaftBlock(NodeID, DateTime.UtcNow, ReceivedTransactionsSinceLastBlock.ToList());
            ReceivedTransactionsSinceLastBlock.Clear();
            return newEntry;
        }

        public override void HandleReceiveVote(RaftVoteResponse voteResponse)
        {
            if (currentTerm < voteResponse.Term)
            {
                TransitionToFollower(voteResponse.Term);
            }
            totalVotesReceived++;
            if (state == RaftState.Candidate && currentTerm == voteResponse.Term)
            {
                if (voteResponse.VoteGranted) { votesForLeaderReceived++; }
                raftNodes.Single(x => x.NodeId == voteResponse.NodeId).VoteGranted = voteResponse.VoteGranted;

                if (votesForLeaderReceived > nodesInNetwork / 2)
                {
                    ElectNodeAsLeader();
                }
                else
                {
                    Thread.Sleep(1);
                    if (eventQueue.Where(e => e is ConsensusEvent consensusEvent && consensusEvent.EventType == ConsensusEventType.ReceiveVote).ToList().Count <= 1)
                    {
                        foreach (RaftNode node in raftNodes)
                        {
                            if (node.VoteGranted == null)
                            {
                                Console.WriteLine($"Node {NodeID} Re-Requested votes from {node.NodeId}");
                                RequestVotes(node.NodeId);
                            }
                        }
                    }
                }
            }
        }

        private void SendHeartBeat(RaftHeartbeatRequest heartbeat)
        {
            eventQueue.Enqueue(new CommunicationEvent(heartbeat, CommunicationEventType.RequestHeartbeat, null));
        }

        private void RequestVotes(int? nodeId = null)
        {
            GetLatestEntryInformation(out var latestBlockIndex, out var latestBlockTerm);
            var voteRequest = new RaftVoteRequest(latestBlockIndex, latestBlockTerm, currentTerm, NodeID);
            eventQueue.Enqueue(new CommunicationEvent(voteRequest, CommunicationEventType.RequestVote, nodeId));
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

            InitializeRaftNodeList(1, 0);
        }

        private void ElectNodeAsLeader()
        {
            GetPreviousEntryInformation(out var previousLogIndex, out int previousElectionTerm);
            InitializeLeader();

            // Request heartbeat:
            var heartbeatRequest = new RaftHeartbeatRequest(currentTerm, NodeID, previousLogIndex, previousElectionTerm, null, commitIndex);
            SendHeartBeat(heartbeatRequest);

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

            InitializeRaftNodeList(BlocksInChain, 0);
        }

        private void GetPreviousEntryInformation(out int previousLogIndex, out int previousElectionTerm)
        {
            blocksSemaphore.Wait();  // I don't think its necessary to acquire the semaphores. There will only be one thread accessing this class at a time
            previousLogIndex = Blocks.FindLastIndex(0, Blocks.Count - 1, x => true);
            previousElectionTerm = (Blocks.ElementAt(previousLogIndex) as RaftBlock)?.ElectionTerm ?? throw new ArgumentException("Previous log entry is wrong block type");
            blocksSemaphore.Release();
        }

        private void InitializeRaftNodeList(int nextIndex, int matchIndex)
        {
            raftNodes.Clear();
            for (int index = 1; index <= nodesInNetwork; index++)
            {
                raftNodes.Add(new RaftNode(index, nextIndex, matchIndex, null));
            }
        }

        #endregion

        public override void HandleRequestVote(RaftVoteRequest voteRequest) // Followers gets this
        {
            bool grantVote = false;
            if (currentTerm < voteRequest.ElectionTerm)
            {
                TransitionToFollower(voteRequest.ElectionTerm);
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
            eventQueue.Enqueue(new CommunicationEvent(new RaftVoteResponse(NodeID, currentTerm, grantVote), CommunicationEventType.CastVote, voteRequest.NodeId));
        }

        public override void HandleRequestHeartBeat(RaftHeartbeatRequest heartbeat) // Followers gets this
        {
            bool success = false;
            Transaction? newTransaction = null;

            if (currentTerm < heartbeat.Term)
            {
                TransitionToFollower(heartbeat.Term);
            }
            if (heartbeat.Term >= currentTerm)
            {
                ResetElectionTimer();

                var previousEntry = Blocks.ElementAtOrDefault(heartbeat.PreviousLogIndex) as RaftBlock;
                if (previousEntry != null)
                {
                    if (previousEntry.ElectionTerm == heartbeat.PreviousLogTerm)
                    {
                        success = true;
                        newTransaction = GenerateNextTransaction();
                    }
                    else
                    {
                        Blocks.RemoveRange(heartbeat.PreviousLogIndex, BlocksInChain - heartbeat.PreviousLogIndex);
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
            eventQueue.Enqueue(new CommunicationEvent(new RaftHeartbeatResponse(NodeID, currentTerm, success, newTransaction), CommunicationEventType.ReceiveHeartbeat, heartbeat.LeaderId));
        }

        private void TransitionToFollower(int term)
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
