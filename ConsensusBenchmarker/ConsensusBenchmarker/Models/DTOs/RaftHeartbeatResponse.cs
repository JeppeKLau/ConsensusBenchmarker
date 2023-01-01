namespace ConsensusBenchmarker.Models.DTOs
{
    public class RaftHeartbeatResponse
    {
        public RaftHeartbeatResponse(int nodeId, int term, bool addedEntry, bool success, Transaction? transaction)
        {
            NodeId = nodeId;
            Term = term;
            AddedEntry = addedEntry;
            Success = success;
            Transaction = transaction;
        }

        public int NodeId { get; set; }

        public int Term { get; set; }

        public bool AddedEntry { get; set; } = false;

        public bool Success { get; set; }

        public Transaction? Transaction { get; set; }
    }
}
