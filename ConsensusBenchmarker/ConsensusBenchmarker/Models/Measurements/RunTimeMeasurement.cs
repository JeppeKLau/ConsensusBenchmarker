using InfluxDB.Client.Core;

namespace ConsensusBenchmarker.Models.Measurements
{
    [Measurement("runtime")]
    public class RunTimeMeasurement : BaseMeasurement
    {
        public RunTimeMeasurement(int nodeId, DateTime timestamp, TimeSpan timeSpan) : base(nodeId, timestamp)
        {
            Seconds = timeSpan.Seconds;
        }

        [Column("Seconds")] public int Seconds { get; set; }
    }
}
