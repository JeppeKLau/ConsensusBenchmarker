namespace ConsensusBenchmarker.Models.DTOs
{
    public class RaftHeartbeatResponse
    {
        public RaftHeartbeatResponse(int nodeId, int term, bool success, Transaction? transaction)
        {
            NodeId = nodeId;
            Term = term;
            Success = success;
            Transaction = transaction;
        }

        public int NodeId { get; set; }

        public int Term { get; set; }

        public bool Success { get; set; }

        public Transaction? Transaction { get; set; }
    }
}
