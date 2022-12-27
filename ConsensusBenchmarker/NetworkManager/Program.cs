using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetworkManager;

static class Program
{
    private static IPAddress? ipAddress;
    private static IPEndPoint? rxEndpoint;
    private static Socket? server;

    private static readonly List<IPAddress> knownNodes = new();

    private static readonly int receivableByteSize = 4096;
    private static readonly int portNumber = 11_000;
    private static readonly string eom = "<|EOM|>";
    private static readonly string dis = "<|DIS|>";

    static async Task Main()
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
        Console.WriteLine($"Listening on {rxEndpoint!.Address}:{rxEndpoint!.Port}\n");
        server!.Bind(rxEndpoint!);
        server!.Listen(1000);

        while (true)
        {
            Socket handler = await server!.AcceptAsync(cancellationToken);
            var rxBuffer = new byte[receivableByteSize];
            var bytesReceived = await handler.ReceiveAsync(rxBuffer, SocketFlags.None, cancellationToken);
            string message = Encoding.UTF8.GetString(rxBuffer, 0, bytesReceived);

            await HandleMessage(message, handler, cancellationToken);
            Console.WriteLine("Status: Number of known nodes is currently: " + knownNodes.Count.ToString());
        }
    }

    private static async Task HandleMessage(string message, Socket handler, CancellationToken cancellationToken = default)
    {
        if (message.Contains(dis) && message.Contains(eom))
        {
            string cleanMessage = message.Remove(message.IndexOf(eom), eom.Length).Remove(0, (message.IndexOf(dis) + dis.Length));
            Console.WriteLine("Recieved message: " + cleanMessage);

            await SendBackListOfKnownNodes(handler, cancellationToken);

            IPAddress newNode = ParseIpAddress(message);
            await BroadcastNewNodeToAllPreviousNodes(newNode, cancellationToken);
            AddNewKnownNode(newNode);
        }
        else
        {
            Console.WriteLine($"Recieved invalid message:\n{message}.\n");
        }
    }

    private static async Task SendBackListOfKnownNodes(Socket handler, CancellationToken cancellationToken = default)
    {
        if (knownNodes.Count > 0)
        {
            var echoBytes = Encoding.UTF8.GetBytes(dis + CreateStringOfKnownNodes() + eom);
            await handler.SendAsync(echoBytes, SocketFlags.None, cancellationToken);
        }
        else
        {
            var echoBytes = Encoding.UTF8.GetBytes(dis + eom);
            await handler.SendAsync(echoBytes, SocketFlags.None, cancellationToken);
        }
    }

    private static string CreateStringOfKnownNodes()
    {
        return string.Join(",", knownNodes.Select(x => "IP:" + x.ToString()));
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

    private static void AddNewKnownNode(IPAddress newNode)
    {
        if (!knownNodes.Contains(newNode))
        {
            knownNodes.Add(newNode);
        }
    }

    private static async Task BroadcastNewNodeToAllPreviousNodes(IPAddress newNode, CancellationToken cancellationToken = default)
    {
        var nodesPendingRemoval = new List<IPAddress>();
        foreach (IPAddress address in knownNodes)
        {
            try
            {
                var echoBytes = Encoding.UTF8.GetBytes(dis + "IP:" + newNode.ToString() + eom);
                var nodeEndpoint = new IPEndPoint(address, portNumber);
                var nodeSocket = new Socket(nodeEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                Console.WriteLine($"Connecting to {address}");
                await nodeSocket.ConnectAsync(nodeEndpoint);
                _ = await nodeSocket.SendAsync(echoBytes, SocketFlags.None, cancellationToken);
                nodeSocket.Shutdown(SocketShutdown.Both);
                Console.WriteLine("Shutting down socket");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Socket had an exception, node has likely crashed. Removing {address} from list.");
                Console.WriteLine(ex);
                nodesPendingRemoval.Add(address);
            }
        }
        nodesPendingRemoval.ForEach(x => knownNodes.Remove(x));
    }
}
