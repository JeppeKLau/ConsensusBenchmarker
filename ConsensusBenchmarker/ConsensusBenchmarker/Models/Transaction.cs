namespace ConsensusBenchmarker.Models
{
    public class Transaction : IEquatable<Transaction>
    {
        public Transaction(int nodeID, int transactionId, DateTime createdAt)
        {
            NodeID = nodeID;
            TransactionId = transactionId;
            CreatedAt = createdAt;
        }

        public int NodeID { get; set; }
        public int TransactionId { get; set; }
        public DateTime CreatedAt { get; set; }

        public bool Equals(Transaction? other)
        {
            return other is not null &&
                   NodeID == other.NodeID &&
                   TransactionId == other.TransactionId;
        }

        public override int GetHashCode() => (NodeID, TransactionId).GetHashCode();
    }
}
