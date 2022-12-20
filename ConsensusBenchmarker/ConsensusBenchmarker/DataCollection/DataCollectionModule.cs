using ConsensusBenchmarker.Models.Events;
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
        private readonly Stack<IEvent> eventStack;
        private readonly int nodeID;
        private readonly Mutex mutex = new();

        private readonly List<Thread> threads = new();
        private bool ExecutionFlag;
        private int blockCount = 0;
        private int transactionCount = 0;

        public DataCollectionModule(ref Stack<IEvent> eventStack, int nodeID)
        {
            currentProcess = Process.GetCurrentProcess();
            this.eventStack = eventStack;
            this.nodeID = nodeID;
            ExecutionFlag = true;
        }

        public async Task CollectData()
        {
            /*
             *  Total CPU usage. (done)
             *  Total storage used. {storage implementation}
             *  Total messages sent and received, as well as their timestamps.
             *  Total time spent verifying blocks and block creation time.
             */
            var mbMemory = 0; // mB memory used
            var cpuTime = 0; // CPU time

            threads.Add(new Thread(() =>
            {
                ReadMemvalue(out mbMemory);
                Console.WriteLine($"Memory: {mbMemory} mB");
            }));

            threads.Add(new Thread(() =>
            {
                ReadCpuValue(out cpuTime);
                Console.WriteLine($"Cpu time: {cpuTime}, block count: {blockCount}");
            }));
            eventStack.Push(new DataCollectionEvent(nodeID, DataCollectionEventType.CollectionReady, null));

            while (ExecutionFlag)
            {
                HandleEvents();
            }
        }

        private static void ReadCpuValue(out int cpuTime)
        {
            var cpuFileStream = File.OpenRead(CPU_STAT_FILE);
            var numRegex = NumberRegex();
            var valueLine = GetLineWithWord("cpu ", cpuFileStream);
            var matches = numRegex.Matches(valueLine);
            cpuTime = matches.Count == 10 ? matches.Take(3).Sum(x => int.Parse(x.Value)) : throw new Exception($"Incorrect match count: {matches.Count}");
            cpuFileStream.Seek(0, SeekOrigin.Begin);
        }

        private void HandleEvents()
        {
            if (eventStack.Peek() is not DataCollectionEvent nextEvent) return;

            switch (nextEvent.EventType)
            {
                case DataCollectionEventType.End:
                    ExecutionFlag = false;
                    break;
                case DataCollectionEventType.BeginBlock:
                    blockCount++;
                    while (threads.Any(x => x.IsAlive)) ;
                    threads.ForEach(x => x.Start());
                    break;
                case DataCollectionEventType.BeginTransaction:
                    transactionCount++;
                    while (threads.Any(x => x.IsAlive)) ;
                    threads.ForEach(x => x.Start());
                    break;
                case DataCollectionEventType.EndBlock:
                case DataCollectionEventType.EndTransaction:
                    while (threads.Any(x => x.IsAlive)) ;
                    threads.ForEach(x => x.Start());
                    break;
                default:
                    throw new ArgumentException($"Unkown type of {nameof(DataCollectionEvent)}", nameof(nextEvent.EventType));
            }
            eventStack.Pop();
        }

        private void ReadMemvalue(out int value)
        {
            var actualMemFile = MEM_STAT_FILE.Replace("{pid}", currentProcess.Id.ToString());
            var memFileStream = File.OpenRead(actualMemFile);
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
