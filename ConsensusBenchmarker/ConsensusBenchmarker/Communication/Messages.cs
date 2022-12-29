using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;
using ConsensusBenchmarker.Models.DTOs;
using Newtonsoft.Json;

namespace ConsensusBenchmarker.Communication
{
    /// <summary>
    /// DEF = Default, DIS = Discover, TRA = Transaction, BLK = Block, QBC = RequestBlockChain, RBC = RecieveBlockChain, EOM = End of Message,
    /// </summary>
    public enum OperationType { DEF = 0, DIS, TRA, BLK, QBC, RBC, EOM };

    public static class Messages
    {
        public static Dictionary<string, OperationType> OperationTypes { get; set; } = PopulateOperationTypes();

        private static Dictionary<string, OperationType> PopulateOperationTypes()
        {
            var OperationTypes = new Dictionary<string, OperationType>();
            foreach (OperationType operation in (OperationType[])Enum.GetValues(typeof(OperationType)))
            {
                OperationTypes.Add(CreateTag(operation), operation);
            }
            return OperationTypes;
        }

        public static string CreateTag(OperationType operation)
        {
            return $"<|{operation}|>";
        }

        public static bool DoesMessageContainOperationTag(string input, OperationType operationType)
        {
            if (input.Contains(CreateTag(operationType)))
            {
                return true;
            }
            return false;
        }

        public static string TrimUntillTag(string input)
        {
            return input.Remove(0, input.IndexOf("<|"));
        }

        public static string RemoveOperationTypeTag(string input, OperationType operationType)
        {
            string tag = CreateTag(operationType);
            return input.Remove(input.IndexOf(tag), tag.Length);
        }

        public static OperationType GetOperationTypeEnum(string input)
        {
            var tryGet = OperationTypes.TryGetValue(input[..7], out var value);
            if (!tryGet) Console.WriteLine($"Operation mapping failed. Input key was: {input[..7]}.\nMethod input was: {input}");
            return value;
        }

        public static string CreateDISMessage(string ipAddress, int nodeId)
        {
            var serialized = JsonConvert.SerializeObject(new KeyValuePair<int, string>(nodeId, ipAddress));
            return $"{CreateTag(OperationType.DIS)}{serialized}{CreateTag(OperationType.EOM)}";
        }

        public static string CreateTRAMessage(Transaction transaction)
        {
            string serializedTransaction = JsonConvert.SerializeObject(transaction);
            return $"{CreateTag(OperationType.TRA)}{serializedTransaction}{CreateTag(OperationType.EOM)}";
        }

        public static string CreateBLOMessage(Block block)
        {
            string serializedBlock = JsonConvert.SerializeObject(block);
            return $"{CreateTag(OperationType.BLK)}{serializedBlock}{CreateTag(OperationType.EOM)}";
        }

        public static string CreateReqBCMessage(int nodeId, string ipAddress)
        {
            var serialized = JsonConvert.SerializeObject(new KeyValuePair<int, string>(nodeId, ipAddress));
            return $"{CreateTag(OperationType.QBC)}{serialized}{CreateTag(OperationType.EOM)}";
        }

        public static string CreateRecBCMessage(List<BlockDTO> blocks)
        {
            string serializedBlocks = string.Empty;
            if (blocks.Any())
            {
                serializedBlocks = JsonConvert.SerializeObject(blocks);
            }
            return $"{CreateTag(OperationType.RBC)}{serializedBlocks}{CreateTag(OperationType.EOM)}";
        }

    }
}
