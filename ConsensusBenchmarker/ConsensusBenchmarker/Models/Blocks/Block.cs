using ConsensusBenchmarker.Communication;
using Newtonsoft.Json;

namespace ConsensusBenchmarker.Models.Blocks
{
    public enum BlockTypes { PoWBlock = 1 }

    [JsonConverter(typeof(BlockConverter))]
    public class Block : IComparable<Block>
    {
        public Block(BlockTypes blockType, int ownerNodeID, DateTime blockCreatedAt, List<Transaction> transactions)
        {
            BlockType = (int)blockType;
            OwnerNodeID = ownerNodeID;
            BlockCreatedAt = blockCreatedAt;
            Transactions = transactions;
        }

        public int BlockType { get; set; }
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
