namespace ConsensusBenchmarker.Models
{
    public class Transaction
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

        public override bool Equals(object? obj)
        {
            return obj is Transaction transaction &&
                   NodeID == transaction.NodeID &&
                   TransactionId == transaction.TransactionId &&
                   CreatedAt.Equals(transaction.CreatedAt);
        }
    }
}
