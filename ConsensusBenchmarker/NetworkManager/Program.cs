using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NetworkManager;

static class Program
{
    private static readonly IPAddress ipAddress = new(new byte[] { 192, 168, 100, 100 });
    private static readonly IPEndPoint rxEndpoint = new(ipAddress, portNumber);
    private static readonly Socket server = new(rxEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

    private static readonly Dictionary<int, string> knownNodes = new();

    private static readonly int receivableByteSize = 4096;
    private static readonly int portNumber = 11_000;
    private static readonly string eom = "<|EOM|>";
    private static readonly string dis = "<|DIS|>";

    private static readonly CancellationTokenSource cancellationTokenSource = new();
    private static readonly System.Timers.Timer timeoutTimer = new(120_000); // 120 sec

    static async Task Main()
    {
        InitializeTimeoutTimer();
        await WaitInstruction(cancellationTokenSource.Token);
    }

    private static void InitializeTimeoutTimer()
    {
        timeoutTimer.AutoReset = false;
        timeoutTimer.Elapsed += (sender, e) =>
        {
            Console.WriteLine("Network Manager timed out, cancellation token will be set to cancel.\n");
            cancellationTokenSource.Cancel();
        };
        timeoutTimer.Start();
    }

    private static void RestartTimer()
    {
        timeoutTimer!.Stop();
        timeoutTimer!.Start();
    }

    private static async Task WaitInstruction(CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Listening on {rxEndpoint.Address}:{rxEndpoint.Port}\n");
        server!.Bind(rxEndpoint);
        server!.Listen(1000);

        while (true)
        {
            try
            {
                var handler = await server!.AcceptAsync(cancellationToken);
                var rxBuffer = new byte[receivableByteSize];
                var bytesReceived = await handler.ReceiveAsync(rxBuffer, SocketFlags.None, cancellationToken);
                var message = Encoding.UTF8.GetString(rxBuffer, 0, bytesReceived);

                await HandleMessage(message, handler, cancellationToken);
                RestartTimer();
                Console.WriteLine("Status: Number of known nodes is currently: " + knownNodes.Count.ToString());
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static async Task HandleMessage(string message, Socket handler, CancellationToken cancellationToken = default)
    {
        if (message.Contains(dis) && message.Contains(eom))
        {
            string cleanMessage = message.Remove(message.IndexOf(eom), eom.Length).Remove(0, (message.IndexOf(dis) + dis.Length));
            Console.WriteLine("Recieved message: " + cleanMessage);

            await SendBackListOfKnownNodes(handler, cancellationToken);

            var newNode = JsonConvert.DeserializeObject<KeyValuePair<int, string>>(cleanMessage);
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
        return JsonConvert.SerializeObject(knownNodes);
    }

    private static void AddNewKnownNode(KeyValuePair<int, string> newNode)
    {
        if (!knownNodes.ContainsKey(newNode.Key))
        {
            knownNodes.Add(newNode.Key, newNode.Value);
        }
    }

    private static async Task BroadcastNewNodeToAllPreviousNodes(KeyValuePair<int, string> newNode, CancellationToken cancellationToken = default)
    {
        var nodesPendingRemoval = new Dictionary<int, string>();
        var messageBytes = Encoding.UTF8.GetBytes(dis + JsonConvert.SerializeObject(newNode) + eom);

        foreach (var node in knownNodes)
        {
            try
            {
                var nodeEndpoint = new IPEndPoint(IPAddress.Parse(node.Value), portNumber);
                var nodeSocket = new Socket(nodeEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                Console.WriteLine($"Connecting to {node.Value}");
                await nodeSocket.ConnectAsync(nodeEndpoint);
                _ = await nodeSocket.SendAsync(messageBytes, SocketFlags.None, cancellationToken);
                nodeSocket.Shutdown(SocketShutdown.Both);
                nodeSocket.Close();
                Console.WriteLine("Shutting down socket");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Socket had an exception, node ({node.Key}, {node.Value}) has likely crashed. Removing it.");
                Console.WriteLine(ex);
                nodesPendingRemoval.Add(node.Key, node.Value);
            }
        }
        foreach (var node in nodesPendingRemoval)
        {
            knownNodes.Remove(node.Key);
        }
    }
}
