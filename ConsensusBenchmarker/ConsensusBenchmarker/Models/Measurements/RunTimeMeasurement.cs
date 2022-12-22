using InfluxDB.Client.Core;

namespace ConsensusBenchmarker.Models.Measurements
{
    [Measurement("runtime")]
    public class RunTimeMeasurement : BaseMeasurement
    {
        public RunTimeMeasurement(int nodeId, DateTime timestamp, TimeSpan timeSpan) : base(nodeId, timestamp)
        {
            TimeSpan = timeSpan;
        }

        [Column("TimeSpan")] public TimeSpan TimeSpan { get; set; }
    }
}
