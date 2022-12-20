using System;

namespace ConsensusBenchmarker.Models
{
    public interface ICloneable<T> { }

    public class Transaction : ICloneable<Transaction>
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
                   TransactionId == transaction.TransactionId;
        }

        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }

        public Transaction Clone()
        {
            return new Transaction(NodeID, TransactionId, CreatedAt);
        }
    }
}
