using ConsensusBenchmarker.Models.Blocks;

namespace ConsensusBenchmarker.Models.DTOs
{
    /// <summary>
    /// AppendEntries DTO. Represents both the addition of new blocks to chain and heartbeats from leader.
    /// </summary>
    public class RaftHeartbeatRequest
    {
        public RaftHeartbeatRequest(bool heartbeat, int term, int leaderId, int previousLogIndex, int previousLogTerm, Block? entries, int leaderCommit)
        {
            Heartbeat = heartbeat;
            Term = term;
            LeaderId = leaderId;
            PreviousLogIndex = previousLogIndex;
            PreviousLogTerm = previousLogTerm;
            Entries = entries;
            LeaderCommit = leaderCommit;
        }

        /// <summary>
        /// Describes if the only purpose of the request to keep the node alive
        /// </summary>
        public bool Heartbeat { get; set; }

        /// <summary>
        /// Leader's term.
        /// </summary>
        public int Term { get; set; }

        /// <summary>
        /// NodeId of the leader. Allows clients to redirect client requests.
        /// </summary>
        public int LeaderId { get; set; }

        /// <summary>
        /// Index of block immediately preceding the new one.
        /// </summary>
        public int PreviousLogIndex { get; set; }

        /// <summary>
        /// Term of previous block.
        /// </summary>
        public int PreviousLogTerm { get; set; }

        /// <summary>
        /// Block to store. Null for heartbeat.
        /// </summary>
        public Block? Entries { get; set; }

        /// <summary>
        /// Leader's commitIndex.
        /// </summary>
        public int LeaderCommit { get; set; }
    }
}
