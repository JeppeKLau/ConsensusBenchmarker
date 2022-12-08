namespace ConsensusBenchmarker.Models.Blocks
{
    public class Block
    {
        public Block(int ownerNodeID, List<Transaction> transactions)
        {
            OwnerNodeID = ownerNodeID;
            BlockCreatedAt = DateTime.Now.ToLocalTime();
            Transactions = transactions;
        }

        public int OwnerNodeID { get; set; }
        public DateTime BlockCreatedAt { get; set; }
        public List<Transaction> Transactions { get; set; } = new List<Transaction>();

    }
}
