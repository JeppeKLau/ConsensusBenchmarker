using InfluxDB.Client.Core;

namespace ConsensusBenchmarker.Models.Measurements
{
    public abstract class BaseMeasurement
    {
        protected BaseMeasurement(int nodeId, DateTime timestamp)
        {
            NodeId = nodeId;
            Timestamp = timestamp;
        }

        [Column("nodeId", IsTag = true)] public int NodeId { get; set; }

        [Column(IsTimestamp = true)] public DateTime Timestamp { get; set; }
    }
}
