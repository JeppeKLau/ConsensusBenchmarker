
using InfluxDB.Client.Core;

namespace ConsensusBenchmarker.Models.Measurements
{
    [Measurement("blocks")]
    public class BlockMeasurement : BaseMeasurement
    {
        public BlockMeasurement(int nodeId, DateTime timestamp, int blockCount) : base(nodeId, timestamp)
        {
            BlockCount = blockCount;
        }

        [Column("Block count")] public int BlockCount { get; set; }
    }
}
