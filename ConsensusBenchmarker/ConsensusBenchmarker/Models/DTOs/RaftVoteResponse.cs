using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsensusBenchmarker.Models.DTOs
{
    public class RaftVoteResponse
    {
        public RaftVoteResponse(int nodeId, int term, bool voteGranted)
        {
            NodeId = nodeId;
            Term = term;
            VoteGranted = voteGranted;
        }

        public int NodeId { get; set; }

        public int Term { get; set; }

        public bool VoteGranted { get; set; }
    }
}
