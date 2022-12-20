using InfluxDB.Client.Core;

namespace ConsensusBenchmarker.Models.Measurements
{
    [Measurement("messages")]
    internal class MessageMeasurement : BaseMeasurement
    {
        public MessageMeasurement(int nodeId, DateTime timestamp, int messageCount) : base(nodeId, timestamp)
        {
            MessageCount = messageCount;
        }

        [Column("Message count")] public int MessageCount { get; set; }
    }
}
