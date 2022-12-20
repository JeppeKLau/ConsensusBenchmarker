namespace ConsensusBenchmarker.Models.Blocks
{
    public class Block
    {
        public Block(int ownerNodeID, DateTime blockCreatedAt, SortedList<(int, int), Transaction> transactions)
        {
            OwnerNodeID = ownerNodeID;
            BlockCreatedAt = blockCreatedAt;
            Transactions = transactions;
        }

        public int OwnerNodeID { get; set; }
        public DateTime BlockCreatedAt { get; set; }
        public SortedList<(int, int), Transaction> Transactions { get; set; } = new SortedList<(int, int), Transaction>();

    }
}
