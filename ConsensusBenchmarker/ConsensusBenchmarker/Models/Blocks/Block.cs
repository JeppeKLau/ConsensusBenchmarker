namespace ConsensusBenchmarker.Models.Blocks
{
    public class Block
    {
        public Block(int ownerNodeID, DateTime blockCreatedAt, List<Transaction> transactions)
        {
            OwnerNodeID = ownerNodeID;
            BlockCreatedAt = blockCreatedAt;
            Transactions = transactions;
        }

        public int OwnerNodeID { get; set; }
        public DateTime BlockCreatedAt { get; set; }
        public List<Transaction> Transactions { get; set; } = new List<Transaction>();

    }
}
