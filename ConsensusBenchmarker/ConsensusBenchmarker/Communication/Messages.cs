using ConsensusBenchmarker.Models;
using System.Net;

namespace ConsensusBenchmarker.Communication
{
    /// <summary>
    /// DEF = Default, DIS = Discover, TRA = Transaction, ACK = Acknoledgement, EOM = End of Message,
    /// </summary>
    public enum OperationType { DEF = 0, DIS, TRA, ACK, EOM };

    public static class Messages
    {
        public static Dictionary<string, OperationType> OperationTypes { get; set; } = PopulateOperationTypes();

        private static Dictionary<string, OperationType> PopulateOperationTypes()
        {
            Dictionary<string, OperationType> OperationTypes = new Dictionary<string, OperationType>();
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

        public static string RemoveOperationTypeTag(string input, OperationType operationType)
        {
            string tag = CreateTag(operationType);
            return input.Remove(input.IndexOf(tag), tag.Length);
        }

        public static OperationType GetOperationTypeEnum(string input)
        {
            var tryGet = OperationTypes.TryGetValue(input[..7], out var value);
            Console.WriteLine($"Operation TryGet: {tryGet}\nInput: {input[0..7]}\n");
            return value;
        }

        public static IPAddress ParseIpAddress(string IPMessage)
        {
            var ipString = IPMessage.Contains("IP:") ? IPMessage[3..] : IPMessage;
            var ipArray = ipString.Split('.');
            _ = byte.TryParse(ipArray[0], out var ip0);
            _ = byte.TryParse(ipArray[1], out var ip1);
            _ = byte.TryParse(ipArray[2], out var ip2);
            _ = byte.TryParse(ipArray[3], out var ip3);
            return new IPAddress(new byte[] { ip0, ip1, ip2, ip3 });
        }

        public static string CreateDISMessage(IPAddress ipAddress)
        {
            return $"{CreateTag(OperationType.DIS)}IP:{ipAddress}{CreateTag(OperationType.EOM)}";
        }

        public static string CreateTRAMessage(Transaction transaction)
        {
            return $"{CreateTag(OperationType.TRA)}{transaction.ToString()}{CreateTag(OperationType.EOM)}";
        }


    }
}
