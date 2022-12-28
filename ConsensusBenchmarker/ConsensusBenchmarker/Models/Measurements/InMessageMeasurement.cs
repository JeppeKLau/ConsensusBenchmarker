using InfluxDB.Client.Core;

namespace ConsensusBenchmarker.Models.Measurements
{
    [Measurement("incoming messages")]
    internal class InMessageMeasurement : BaseMeasurement
    {
        public InMessageMeasurement(int nodeId, DateTime timestamp, int messageCount) : base(nodeId, timestamp)
        {
            MessageCount = messageCount;
        }

        [Column("Message count")] public int MessageCount { get; set; }
    }
}
