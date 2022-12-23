namespace ConsensusBenchmarker.Models.Blocks
{
    public class Block : IComparable<Block>
    {
        public Block(int ownerNodeID, DateTime blockCreatedAt, List<Transaction> transactions)
        {
            OwnerNodeID = ownerNodeID;
            BlockCreatedAt = blockCreatedAt;
            Transactions = transactions;
        }

        public int OwnerNodeID { get; set; }
        public DateTime BlockCreatedAt { get; set; }
        public List<Transaction> Transactions { get; set; }

        public int CompareTo(Block? other)
        {
            return other is not null ? this.BlockCreatedAt.CompareTo(other.BlockCreatedAt) : 1;
        }

        public override string ToString()
        {
            return nameof(OwnerNodeID) + " : " + OwnerNodeID + "\n" +
                nameof(BlockCreatedAt) + " : " + BlockCreatedAt.ToString() + "\n" +
                nameof(Transactions) + " : " + string.Join(",", Transactions).Replace("\n", "\n\t") + "\n";
        }

    }
}
