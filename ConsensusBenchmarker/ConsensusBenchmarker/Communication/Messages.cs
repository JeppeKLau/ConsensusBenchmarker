using System.Net;

namespace ConsensusBenchmarker.Communication
{
    public enum OperationType { Default = 0, Discover, EOM, ACK };

    public static class Messages
    {
        public static readonly string EOM = "<|EOM|>";
        public static readonly string Discover = "<|DIS|>";
        public static readonly string ACK = "<|ACK|>";

        public static string GiveMeYourKnownNodesMessage(IPAddress ipAddress)
        {
            return $"{Discover}, {ipAddress} {EOM}";
        }

        public static bool IsMessageValid(string response)
        {
            if (response.Contains(EOM))
            {
                return true;
            }
            return false;
        }

        public static OperationType GetMessageType(string response)
        {
            return OperationType.Default;
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
    }
}
