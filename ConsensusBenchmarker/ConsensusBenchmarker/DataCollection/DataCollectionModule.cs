using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace ConsensusBenchmarker.DataCollection
{
    public class DataCollectionModule
    {
        private static readonly string CPU_STAT_FILE = "/proc/stat";         // cpu <10 numbers> \n
        private static readonly string MEM_STAT_FILE = "/proc/{pid}/status"; // VmSize: <some number of characters> \n
        private readonly Process currentProcess;

        public DataCollectionModule()
        {
            currentProcess = Process.GetCurrentProcess();
        }

        public async Task CollectData()
        {
            var actualMemFile = MEM_STAT_FILE.Replace("{pid}", currentProcess.Id.ToString());
            var memFileStream = File.OpenRead(actualMemFile);
            var memValue = 0; // MB memory used

            var memThread = new Thread(() =>
            {
                ReadMemvalue(memFileStream, out memValue);
                Thread.Sleep(1000);
                Console.WriteLine(memValue.ToString());
            });
            memThread.Start();

        }

        private static void ReadMemvalue(FileStream file, out int value)
        {
            var numRegex = new Regex(@"[0-9]+");
            var valueLine = GetLineWithWord("VmSize:", file);
            var sizeDenominator = valueLine.Substring(valueLine.Length - 2, 2);
            var matches = numRegex.Matches(valueLine);
            var numberString = matches.Count == 1 ? matches.First().Value : throw new Exception("Too many matches in memory file");
            var dividend = sizeDenominator == "kB" ? 1000 : sizeDenominator == "mB" ? 1 : sizeDenominator == "gB" ? 0.001 : 0;

            Console.WriteLine($"valueLine: {valueLine}");
            Console.WriteLine($"sizeDenominator: {sizeDenominator}");
            Console.WriteLine($"numberString: {numberString}");
            Console.WriteLine($"dividend: {dividend}");

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
                    Console.WriteLine($"Line: {line}");
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
    }
}
