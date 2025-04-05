using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace CapFrameX.PMD.Benchlab
{
    public class BenchlabPmdService
    {
        public async Task<List<Sensor>> GetUpdatedSensorListAsync()
        {
            var json = string.Empty;
            using (var client = new NamedPipeClientStream(".", "BenchlabSensorPipe", PipeDirection.InOut))
            {
                await client.ConnectAsync();

                var writer = new StreamWriter(client) { AutoFlush = true };
                var reader = new StreamReader(client);

                await writer.WriteLineAsync("GetUpdatedSensorList");
                json = await reader.ReadLineAsync();

                // Do not dispose writer/reader separately — they share the pipe stream.
                // The client.Dispose() at the end will clean them up safely.
            }

            var sensorList = JsonConvert.DeserializeObject<List<Sensor>>(json);
            return sensorList ?? new List<Sensor>();
        }

        public List<Sensor> GetUpdatedSensorList()
        {
            var json = string.Empty;
            using (var client = new NamedPipeClientStream(".", "BenchlabSensorPipe", PipeDirection.InOut))
            {
                client.Connect();

                var writer = new StreamWriter(client) { AutoFlush = true };
                var reader = new StreamReader(client);

                writer.WriteLine("GetUpdatedSensorList");
                json = reader.ReadLine();

                // Do not dispose writer/reader separately — they share the pipe stream.
                // The client.Dispose() at the end will clean them up safely.
            }

            var sensorList = JsonConvert.DeserializeObject<List<Sensor>>(json);
            return sensorList ?? new List<Sensor>();
        }
    }
}
