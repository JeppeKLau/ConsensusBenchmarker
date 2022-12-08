using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsensusBenchmarker.Models.Blocks
{
    public class Block
    {   
        public Block(uint ownerNodeID, List<Transaction> transactions)
        {
            OwnerNodeID = ownerNodeID;
            BlockCreatedAt = DateTime.Now.ToLocalTime(); 
            Transactions = transactions;
        }

        public uint OwnerNodeID { get; set; }
        public DateTime BlockCreatedAt { get; set; }
        public List<Transaction> Transactions { get; set; } = new List<Transaction>();
        
    }
}
