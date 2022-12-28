using ConsensusBenchmarker.Models.Events;
using ConsensusBenchmarker.Models.Measurements;
using InfluxDB.Client.Api.Domain;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace ConsensusBenchmarker.DataCollection
{
    public partial class DataCollectionModule
    {
        private static readonly string CPU_STAT_FILE = "/proc/stat";         // cpu <10 numbers> \n
        private static readonly string MEM_STAT_FILE = "/proc/{pid}/status"; // VmSize: <some number of characters> \n
        private readonly Process currentProcess;
        private readonly ConcurrentQueue<IEvent> eventQueue;
        private readonly int nodeId;
        private readonly InfluxDBService influxDBService;
        private readonly DateTime startTime;
        private Thread? memThread;
        private bool executionFlag;
        private int blockCount = 0;
        private int transactionCount = 0;
        private int inMessageCount = 0;
        private int outMessageCount = 0;
        private SemaphoreSlim counterSemaphore = new SemaphoreSlim(1, 1);

        public DataCollectionModule(ref ConcurrentQueue<IEvent> eventQueue, int nodeID, InfluxDBService influxDBService, DateTime startTime)
        {
            currentProcess = Process.GetCurrentProcess();
            this.eventQueue = eventQueue;
            this.nodeId = nodeID;
            this.influxDBService = influxDBService;
            this.startTime = startTime;
            executionFlag = true;
        }

        public void SpawnThreads(Dictionary<string, Thread> moduleThreads)
        {
            moduleThreads.Add("DataCollection_CollectData", new Thread(CollectData));
            memThread = new Thread(() =>
            {
                while (executionFlag)
                {
                    ReadMemvalue(out int mbMemory);
                    WriteInformationToDB(new MemoryMeasurement(nodeId, DateTime.UtcNow, mbMemory));
                    Thread.Sleep(1000);
                }
            });
            moduleThreads.Add("DataCollection_ReadMemory", memThread);

            eventQueue.Enqueue(new DataCollectionEvent(nodeId, DataCollectionEventType.CollectionReady, null));
        }

        private void CollectData()
        {
            /*
             *  _Total CPU usage.
             *  Total storage used. {storage implementation}
             *      - How do we write blocks to storage?
             *          - Do we write blocks to permanent storage? - No
             *      - Where do we store the blocks?
             *  _Total messages sent and received, as well as their timestamps.
             *      - Count messages transmitted to and from node
             *          - 
             *  _Total time spent verifying blocks and block creation time. 
             */

            while (executionFlag || !eventQueue.IsEmpty)
            {
                HandleEvents();
                Thread.Sleep(1);
            }

            var endTime = DateTime.UtcNow;
            ReadCpuValue(out int cpuTime);
            WriteInformationToDB(new CPUMeasurement(nodeId, DateTime.UtcNow, cpuTime));
            WriteInformationToDB(new RunTimeMeasurement(nodeId, DateTime.UtcNow, endTime.Subtract(startTime)));
            WriteInformationToDB(new TransactionMeasurement(nodeId, DateTime.UtcNow, transactionCount));
            WriteInformationToDB(new InMessageMeasurement(nodeId, DateTime.UtcNow, inMessageCount));
            memThread?.Join();
        }

        private void WriteInformationToDB(BaseMeasurement measurement)
        {
            influxDBService.Write(write =>
            {
                write.WriteMeasurement(measurement, WritePrecision.Ns, "primary", "MastersThesis");
            });
        }

        private static void ReadCpuValue(out int cpuTime)
        {
            var cpuFileStream = System.IO.File.OpenRead(CPU_STAT_FILE);
            var numRegex = NumberRegex();
            var valueLine = GetLineWithWord("cpu ", cpuFileStream);
            var matches = numRegex.Matches(valueLine);
            cpuTime = matches.Count == 10 ? matches.Take(3).Sum(x => int.Parse(x.Value)) : throw new Exception($"Incorrect match count: {matches.Count}");
            cpuFileStream.Seek(0, SeekOrigin.Begin);
        }

        private void HandleEvents()
        {
            if (!eventQueue.TryPeek(out var @event)) return;
            if (@event is not DataCollectionEvent nextEvent) return;

            counterSemaphore.Wait();

            switch (nextEvent.EventType)
            {
                case DataCollectionEventType.End:
                    executionFlag = false;
                    Console.WriteLine("Data collection was signalled to end.");
                    break;
                case DataCollectionEventType.IncBlock:
                    blockCount++;
                    var blockCreationTime = nextEvent.Data as Stopwatch ?? new Stopwatch();
                    WriteInformationToDB(new BlockMeasurement(nodeId, DateTime.UtcNow, blockCount, blockCreationTime.ElapsedMilliseconds / 1000));
                    break;
                case DataCollectionEventType.IncTransaction:
                    transactionCount++;
                    WriteInformationToDB(new TransactionMeasurement(nodeId, DateTime.Now, transactionCount));
                    break;
                case DataCollectionEventType.IncMessage:
                    inMessageCount++;
                    WriteInformationToDB(new InMessageMeasurement(nodeId, DateTime.UtcNow, inMessageCount));
                    break;
                case DataCollectionEventType.OutMessage:
                    outMessageCount++;
                    WriteInformationToDB(new OutMessageMeasurement(nodeId, DateTime.UtcNow, outMessageCount));
                    break;
                default:
                    throw new ArgumentException($"Unkown type of {nameof(DataCollectionEvent)}", nameof(nextEvent.EventType));
            }
            eventQueue.TryDequeue(out _);
            counterSemaphore.Release();
        }

        private void ReadMemvalue(out int value)
        {
            var actualMemFile = MEM_STAT_FILE.Replace("{pid}", currentProcess.Id.ToString());
            var memFileStream = System.IO.File.OpenRead(actualMemFile);
            var numRegex = NumberRegex();
            var valueLine = GetLineWithWord("VmSize:", memFileStream);
            var sizeDenominator = valueLine.Substring(valueLine.Length - 2, 2);
            var matches = numRegex.Matches(valueLine);
            var numberString = matches.Count == 1 ? matches.First().Value : throw new Exception("Too many matches in memory file");
            var dividend = sizeDenominator == "kB" ? 1000 : sizeDenominator == "mB" ? 1 : sizeDenominator == "gB" ? 0.001 : 0;

            var tryParse = int.TryParse(numberString, out value);
            value = (int)(value / dividend);

            if (!tryParse)
            {
                throw new FormatException("Could not parse number from string");
            }
            memFileStream.Seek(0, SeekOrigin.Begin);
        }

        private static string GetLineWithWord(string word, FileStream file)
        {
            var fileStreamReader = new StreamReader(file);
            var lines = ReadLineTillDelimiter(fileStreamReader, '\n');

            foreach (var line in lines)
            {
                if (line.Contains(word))
                {
                    return line;
                }
            }

            throw new ArgumentException($"File does not contain word {word} or line endings", nameof(file));
        }

        private static IEnumerable<string> ReadLineTillDelimiter(TextReader reader, char delimiter)
        {
            var stringBuilder = new StringBuilder();
            while (reader.Peek() > -1)
            {
                var c = (char)reader.Read();

                if (c == delimiter)
                {
                    yield return stringBuilder.ToString();
                    stringBuilder.Clear();
                    continue;
                }

                stringBuilder.Append(c);
            }
        }

        [GeneratedRegex("[0-9]+")]
        private static partial Regex NumberRegex();
    }
}
