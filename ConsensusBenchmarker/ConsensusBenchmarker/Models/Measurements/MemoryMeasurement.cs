using InfluxDB.Client.Core;

namespace ConsensusBenchmarker.Models.Measurements
{
    [Measurement("memory")]
    public class MemoryMeasurement : BaseMeasurement
    {
        public MemoryMeasurement(int nodeId, DateTime timestamp, int megabytes) :
            base(nodeId, timestamp)
        {
            Megabytes = megabytes;
        }

        [Column("mB")] public int Megabytes { get; set; }
    }
}
