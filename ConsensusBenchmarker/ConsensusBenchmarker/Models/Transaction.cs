using ConsensusBenchmarker.Communication;
using System.Net;

namespace ConsensusBenchmarker.Models
{
    public class Transaction
    {
        public Transaction(string transaction)
        {
            string[] transactionElements = transaction.Split(';');
            if(transactionElements.Length == 3)
            {
                Sender = Messages.ParseIpAddress(transactionElements[0]);
                TransactionId = int.Parse(transactionElements[1]);
                CreatedAt = DateTime.Parse(transactionElements[2]);
            }
            else
            {
                throw new ArgumentException("Transaction could not be created because the input was wrong.");
            }
        }

        public Transaction(IPAddress sender, int transactionId, DateTime createdAt)
        {
            Sender = sender;
            TransactionId = transactionId;
            CreatedAt = createdAt;
        }

        public IPAddress Sender { get; set; }
        public int TransactionId { get; set; }
        public DateTime CreatedAt { get; set; }

        public override string ToString()
        {
            return $"{Sender};{TransactionId};{CreatedAt}";
        }
    }
}
