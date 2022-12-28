using ConsensusBenchmarker.Models.Blocks;

namespace ConsensusBenchmarker.Models.DTOs
{
    public class BlockDTO
    {
        public BlockDTO(Block block, long blockchainLength)
        {
            Block = block;
            BlockchainLength = blockchainLength;
        }

        public Block Block { get; set; }

        public long BlockchainLength { get; set; }
    }
}
