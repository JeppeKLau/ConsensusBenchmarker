using System.Diagnostics;
using System.Text;

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
            var valueLine = GetLineWithWord("VmSize:", file);
            var sizeDenominator = valueLine.Substring(valueLine.IndexOf('\n') - 2, 2);
            var numberString = valueLine.Substring(valueLine.IndexOf("VmSize:") + "VmSize:".Length, valueLine.IndexOf(sizeDenominator));
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
            var startIndex = -1;
            var stringBuilder = new StringBuilder();
            foreach (var chars in ReadFileInChunks(file))
            {
                Console.WriteLine($"chars: {chars}");
                if (startIndex > -1 && chars.Contains('\n', StringComparison.CurrentCulture))
                {
                    return stringBuilder.ToString();
                }

                if (startIndex > -1 || (startIndex = chars.IndexOf(word)) > -1)
                {
                    stringBuilder.Append(chars);
                }
            }

            throw new ArgumentException($"File does not contain word {word} or line endings", nameof(file));
        }

        private static IEnumerable<string> ReadFileInChunks(FileStream file, int chunkSize = 7)
        {
            byte[] buffer = new byte[chunkSize];
            int currentRead;
            while ((currentRead = file.Read(buffer, 0, chunkSize)) > 0)
            {
                yield return Encoding.UTF8.GetString(buffer, 0, currentRead);
            }
        }
    }
}
