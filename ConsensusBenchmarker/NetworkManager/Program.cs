// See https://aka.ms/new-console-template for more information


using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetworkManager;

static class Program
{
    private static IPAddress ipAddress;
    private static IPEndPoint rxEndpoint;
    private static Socket server;
    private static uint receivableByteSize = 4096;
    private static readonly int portNumber = 11_000;

    private static List<IPAddress> knownNodes = new List<IPAddress>();

    public static readonly string eom = "<|EOM|>";
    private static readonly string ack = "<|ACK|>";
    private static readonly string discover = "<|DIS|>";

    static async Task Main(string[] args)
    {
        Initialize();
        await WaitInstruction();
    }

    private static void Initialize()
    {
        ipAddress = IPAddress.Loopback;
        rxEndpoint = new(ipAddress, portNumber);
        server = new Socket(rxEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        //knownNodes.Add(ipAddress);
    }

    private static async Task WaitInstruction(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Listening on {rxEndpoint.Address}:{rxEndpoint.Port}");
        server.Bind(rxEndpoint);
        server.Listen(1000);

        while (true)
        {
            Socket handler = await server.AcceptAsync(cancellationToken);
            var rxBuffer = new byte[receivableByteSize];
            var bytesReceived = await handler.ReceiveAsync(rxBuffer, SocketFlags.None, cancellationToken);
            string message = Encoding.UTF8.GetString(rxBuffer, 0, bytesReceived);

            Console.WriteLine(message);
            await HandleMessage(message, handler, cancellationToken);
        }
    }

    private static async Task HandleMessage(string message, Socket handler, CancellationToken cancellationToken = default)
    {
        // Message = "<|DIS|>IP:[ip of node]<|EOM|>"
        if (message.Contains(eom) && message.Contains(discover))
        {
            AddNewKnownNode(message.Remove(0,discover.Length));
            await SendBackListOfKnownNodes(handler, cancellationToken);
            await BroadcastNewNodeToAllPreviousNodes(handler, cancellationToken);
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
        var ipString = message[3..message.IndexOf(eom)];
        var ipArray = ipString.Split('.');
        _ = byte.TryParse(ipArray[0], out var ip0);
        _ = byte.TryParse(ipArray[1], out var ip1);
        _ = byte.TryParse(ipArray[2], out var ip2);
        _ = byte.TryParse(ipArray[3], out var ip3);
        return new IPAddress(new byte[] { ip0, ip1, ip2, ip3 });
    }

    private static async Task SendBackListOfKnownNodes(Socket handler, CancellationToken cancellationToken = default)
    {
        var echoBytes = Encoding.UTF8.GetBytes(ack + CreateStringOfKnownNodes() + eom);
        await handler.SendAsync(echoBytes, SocketFlags.None, cancellationToken);
        Console.WriteLine($"Socket server sent back list of known nodes.\n\n");
    }

    private static string CreateStringOfKnownNodes()
    {
        return string.Join(",", knownNodes.Select(x => x.ToString()));
    }

    private static async Task BroadcastNewNodeToAllPreviousNodes(Socket handler, CancellationToken cancellationToken = default)
    {
        IPAddress newNode = knownNodes.Last();
        foreach (IPAddress address in knownNodes)
        {
            if(address != newNode)
            {
                var echoBytes = Encoding.UTF8.GetBytes(discover + newNode.ToString() + eom); 
                var networkManagerEndpoint = new IPEndPoint(address, portNumber);
                var networkManager = new Socket(networkManagerEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                await networkManager.ConnectAsync(networkManagerEndpoint);
                _ = await networkManager.SendAsync(echoBytes, SocketFlags.None, cancellationToken);
                await handler.SendAsync(echoBytes, SocketFlags.None, cancellationToken);
            }
        }
    }
}
