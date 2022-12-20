
using InfluxDB.Client.Core;

namespace ConsensusBenchmarker.Models.Measurements
{
    [Measurement("transactions")]
    public class TransactionMeasurement : BaseMeasurement
    {
        public TransactionMeasurement(int nodeId, DateTime timestamp, int transactionCount) : base(nodeId, timestamp)
        {
            TransactionCount = transactionCount;
        }

        [Column("Transaction count")] public int TransactionCount { get; set; }
    }
}
