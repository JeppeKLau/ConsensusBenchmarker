using ConsensusBenchmarker.Models;
using ConsensusBenchmarker.Models.Blocks;
using ConsensusBenchmarker.Models.DTOs;
using Newtonsoft.Json;

namespace ConsensusBenchmarker.Communication
{
    /// <summary>
    /// DEF = Default, DIS = Discover, TRA = Transaction, BLK = Block, RQB = RequestBlockChain, RCB = RecieveBlockChain, RQV = RequestVote, RCV = ReceiveVote, RQH = RequestHeartBeat, RCH = ReceiveHeartBeat, EOM = End of Message,
    /// </summary>
    public enum OperationType { DEF = 0, DIS, TRA, BLK, RQB, RCB, RQV, RCV, RQH, RCH, EOM };

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

        public static string CreateReqBCMessage(int nodeId)
        {
            return $"{CreateTag(OperationType.RQB)}{nodeId}{CreateTag(OperationType.EOM)}";
        }

        public static string CreateRecBCMessage(List<Block> blocks)
        {
            string serializedBlocks = string.Empty;
            if (blocks.Any())
            {
                serializedBlocks = JsonConvert.SerializeObject(blocks);
            }
            return $"{CreateTag(OperationType.RCB)}{serializedBlocks}{CreateTag(OperationType.EOM)}";
        }

        public static string CreateRQVMessage(RaftVoteRequest voteRequest)
        {
            var serializedVoteRequest = JsonConvert.SerializeObject(voteRequest);
            return $"{CreateTag(OperationType.RQV)}{serializedVoteRequest}{CreateTag(OperationType.EOM)}";
        }

        public static string CreateRCVMessage(RaftVoteResponse voteResponse)
        {
            var serializedVoteResponse = JsonConvert.SerializeObject(voteResponse);
            return $"{CreateTag(OperationType.RCV)}{serializedVoteResponse}{CreateTag(OperationType.EOM)}";
        }

        public static string CreateRQHMessage(RaftHeartbeatRequest heartbeatRequest)
        {
            var serializedHeartbeatRequest = JsonConvert.SerializeObject(heartbeatRequest);
            return $"{CreateTag(OperationType.RQH)}{serializedHeartbeatRequest}{CreateTag(OperationType.EOM)}";
        }

        public static string CreateRCHMessage(RaftHeartbeatResponse heartbeatResponse)
        {
            var serializedHeartbeatResponse = JsonConvert.SerializeObject(heartbeatResponse);
            return $"{CreateTag(OperationType.RCH)}{serializedHeartbeatResponse}{CreateTag(OperationType.EOM)}";
        }

    }
}
