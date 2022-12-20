using InfluxDB.Client.Core;

namespace ConsensusBenchmarker.Models.Measurements
{
    [Measurement("cpu")]
    public class CPUMeasurement : BaseMeasurement
    {
        public CPUMeasurement(int nodeId, DateTime timestamp, int cPUTime) : base(nodeId, timestamp)
        {
            CPUTime = cPUTime;
        }

        [Column("Time")] public int CPUTime { get; set; }
    }
}
