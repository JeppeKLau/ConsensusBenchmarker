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
        private bool MemFlag = true;
        private bool CpuFlag = true;
        private readonly Stack<IEvent> eventStack;
        private readonly int nodeID;
        private readonly Mutex mutex = new();

        public DataCollectionModule(ref Stack<IEvent> eventStack, int nodeID)
        {
            currentProcess = Process.GetCurrentProcess();
            this.eventStack = eventStack;
            this.nodeID = nodeID;
        }

        public async Task CollectData()
        {
            var actualMemFile = MEM_STAT_FILE.Replace("{pid}", currentProcess.Id.ToString());
            var memFileStream = File.OpenRead(actualMemFile);
            var mbMemory = 0; // MB memory used
            var cpuFileStream = File.OpenRead(CPU_STAT_FILE);
            var cpuTime = 0; // CPU time
            eventStack.Push(new DataCollectionEvent(nodeID, DataCollectionEventType.CollectionReady));

            var memThread = new Thread(() =>
            {
                while (MemFlag)
                {
                    ReadMemvalue(memFileStream, out mbMemory);
                    Console.WriteLine(mbMemory.ToString());
                    Thread.Sleep(1000);
                    UpdateExecutionFlag();
                }
            });

            var cpuThread = new Thread(() =>
            {
                while (CpuFlag)
                {
                    ReadCpuValue(cpuFileStream, out cpuTime);
                    Console.WriteLine(cpuTime.ToString());
                    Thread.Sleep(1000);
                    UpdateExecutionFlag();
                }
            });

            memThread.Start();
            cpuThread.Start();
        }

        private void ReadCpuValue(FileStream cpuFileStream, out int cpuTime)
        {
            var numRegex = NumberRegex();
            var valueLine = GetLineWithWord("cpu ", cpuFileStream);
            var matches = numRegex.Matches(valueLine);
            cpuTime = matches.Count == 10 ? matches.Take(3).Sum(x => int.Parse(x.Value)) : throw new Exception($"Incorrect match count: {matches.Count}");
        }

        private void UpdateExecutionFlag()
        {
            while (!mutex.WaitOne()) ;
            if (eventStack.Peek() is not DataCollectionEvent nextEvent) return;

            if (nextEvent.DataCollectionEventType == DataCollectionEventType.End)
            {
                MemFlag = false;
                CpuFlag = false;
                eventStack.Pop();
            }
        }

        private static void ReadMemvalue(FileStream file, out int value)
        {
            var numRegex = NumberRegex();
            var valueLine = GetLineWithWord("VmSize:", file);
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
