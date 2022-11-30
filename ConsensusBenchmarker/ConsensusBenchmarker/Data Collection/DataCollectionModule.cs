using System.Diagnostics;
using System.Text;

namespace ConsensusBenchmarker.Data_Collection
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
        }

        private IEnumerable<string> ReadFileInChunks(FileStream file, int chunkSize = 7)
        {
            byte[] buffer = new byte[chunkSize];
            int currentRead;
            while ((currentRead = file.Read(buffer, 0, chunkSize)) > 0)
            {
                yield return Encoding.UTF8.GetString(buffer, 0, currentRead);
            }
        }

        private string GetLineAfterWord(string word, FileStream file)
        {
            var startIndex = -1;
            var stringBuilder = new StringBuilder();
            foreach (var chars in ReadFileInChunks(file))
            {
                if (startIndex >= 0 && chars.Contains('\n', StringComparison.CurrentCulture))
                {
                    return stringBuilder.ToString();
                }

                if ((startIndex = chars.IndexOf(word)) >= 0)
                {
                    stringBuilder.Append(chars);
                }
            }
            throw new ArgumentException($"File does not contain word {word}", nameof(file));
        }

    }
}
