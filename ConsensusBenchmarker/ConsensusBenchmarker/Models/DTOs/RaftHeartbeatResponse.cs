namespace ConsensusBenchmarker.Models.DTOs
{
    public class RaftHeartbeatResponse
    {
        public RaftHeartbeatResponse(int term, bool success)
        {
            Term = term;
            Success = success;
        }

        public int Term { get; set; }

        public bool Success { get; set; }
    }
}
