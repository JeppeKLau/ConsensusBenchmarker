
using InfluxDB.Client.Core;

namespace ConsensusBenchmarker.Models.Measurements
{
    [Measurement("blocks")]
    public class BlockMeasurement : BaseMeasurement
    {
        public BlockMeasurement(int nodeId, DateTime timestamp, int blockCount, DateTime creationTime) : base(nodeId, timestamp)
        {
            BlockCount = blockCount;
            CreationTime = creationTime;
        }

        [Column("Block count")] public int BlockCount { get; set; }

        [Column("Creation time")] public DateTime CreationTime { get; set; }
    }
}
