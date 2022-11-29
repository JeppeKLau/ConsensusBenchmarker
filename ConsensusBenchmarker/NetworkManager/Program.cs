using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetworkManager;

static class Program
{
    private static IPAddress? ipAddress;
    private static IPEndPoint? rxEndpoint;
    private static Socket? server;

    private static List<IPAddress> knownNodes = new List<IPAddress>();

    private static readonly int receivableByteSize = 4096;
    private static readonly int portNumber = 11_000;
    private static readonly string eom = "<|EOM|>";
    private static readonly string ack = "<|ACK|>";
    private static readonly string discover = "<|DIS|>";

    static async Task Main(string[] args)
    {
        Initialize();
        await WaitInstruction();
    }

    private static void Initialize()
    {
        ipAddress = new IPAddress(new byte[] { 192, 168, 100, 100 });
        rxEndpoint = new(ipAddress!, portNumber);
        server = new Socket(rxEndpoint!.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
    }

    private static async Task WaitInstruction(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Listening on {rxEndpoint!.Address}:{rxEndpoint!.Port}");
        server!.Bind(rxEndpoint!);
        server!.Listen(1000);

        while (true)
        {
            Socket handler = await server!.AcceptAsync(cancellationToken);
            var rxBuffer = new byte[receivableByteSize];
            var bytesReceived = await handler.ReceiveAsync(rxBuffer, SocketFlags.None, cancellationToken);
            string message = Encoding.UTF8.GetString(rxBuffer, 0, bytesReceived);

            Console.WriteLine("Recieved message: " + message); // TEMP
            await HandleMessage(message, handler, cancellationToken);
            Console.WriteLine("Status: Number of known nodes is currently: " + knownNodes.Count.ToString());
        }
    }

    private static async Task HandleMessage(string message, Socket handler, CancellationToken cancellationToken = default)
    {
        if (message.Contains(discover) && message.Contains(eom))
        {
            string cleanMessage = message.Remove(message.IndexOf(eom), eom.Length).Remove(0, discover.Length);
            Console.WriteLine(cleanMessage); // TEMP

            await SendBackListOfKnownNodes(handler, cancellationToken);
            AddNewKnownNode(cleanMessage);
            await BroadcastNewNodeToAllPreviousNodes(cancellationToken);
        }
    }

    private static void AddNewKnownNode(string message)
    {
        IPAddress newIP = ParseIpAddress(message);
        if (!knownNodes.Contains(newIP))
        {
            knownNodes.Add(newIP);
        }
    }

    private static IPAddress ParseIpAddress(string message)
    {
        var ipString = message.Contains("IP:") ? message[3..] : message;
        var ipArray = ipString.Split('.');
        _ = byte.TryParse(ipArray[0], out var ip0);
        _ = byte.TryParse(ipArray[1], out var ip1);
        _ = byte.TryParse(ipArray[2], out var ip2);
        _ = byte.TryParse(ipArray[3], out var ip3);
        return new IPAddress(new byte[] { ip0, ip1, ip2, ip3 });
    }

    private static async Task SendBackListOfKnownNodes(Socket handler, CancellationToken cancellationToken = default)
    {
        if(knownNodes.Count > 0)
        {
            var echoBytes = Encoding.UTF8.GetBytes(ack + CreateStringOfKnownNodes() + eom);
            await handler.SendAsync(echoBytes, SocketFlags.None, cancellationToken);
            Console.WriteLine($"Socket server sent back list of known nodes.\n\n"); // TEMP
        }
    }

    private static string CreateStringOfKnownNodes()
    {
        return string.Join(",", knownNodes.Select(x => "IP:" + x.ToString()));
    }

    private static async Task BroadcastNewNodeToAllPreviousNodes(CancellationToken cancellationToken = default)
    {
        IPAddress newNode = knownNodes.Last();
        foreach (IPAddress address in knownNodes)
        {
            if (address != newNode)
            {
                var echoBytes = Encoding.UTF8.GetBytes(discover + "IP:" + newNode.ToString() + eom);
                var nodeEndpoint = new IPEndPoint(address, portNumber);
                var nodeSocket = new Socket(nodeEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                await nodeSocket.ConnectAsync(nodeEndpoint);
                _ = await nodeSocket.SendAsync(echoBytes, SocketFlags.None, cancellationToken);
                nodeSocket.Shutdown(SocketShutdown.Both);
            }
        }
    }
}
