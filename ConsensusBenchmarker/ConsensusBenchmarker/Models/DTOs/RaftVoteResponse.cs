using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsensusBenchmarker.Models.DTOs
{
    public class RaftVoteResponse
    {
        public RaftVoteResponse(int term, bool voteGranted)
        {
            Term = term;
            VoteGranted = voteGranted;
        }

        public int Term { get; set; }

        public bool VoteGranted { get; set; }
    }
}
