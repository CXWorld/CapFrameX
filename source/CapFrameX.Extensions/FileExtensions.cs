using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Extensions
{
    public class FileExtensions
    {
        public static async Task<string> ReadAllTextAsync(string filePath)
        {
            var stringBuilder = new StringBuilder();
            using (var fileStream = File.OpenRead(filePath))
            using (var streamReader = new StreamReader(fileStream))
            {
                string line = await streamReader.ReadLineAsync();
                while (line != null)
                {
                    stringBuilder.AppendLine(line);
                    line = await streamReader.ReadLineAsync();
                }
                return stringBuilder.ToString();
            }
        }
    }
}
