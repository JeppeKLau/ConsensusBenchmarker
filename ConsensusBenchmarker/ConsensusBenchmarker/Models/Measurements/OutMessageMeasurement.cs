using InfluxDB.Client.Core;

namespace ConsensusBenchmarker.Models.Measurements
{
    [Measurement("outgoing messages")]
    internal class OutMessageMeasurement : BaseMeasurement
    {
        public OutMessageMeasurement(int nodeId, DateTime timestamp, int messageCount) : base(nodeId, timestamp)
        {
            MessageCount = messageCount;
        }

        [Column("Message count")] public int MessageCount { get; set; }
    }
}
