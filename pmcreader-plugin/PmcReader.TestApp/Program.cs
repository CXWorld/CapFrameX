using CapFrameX.PmcReader.Plugin;
using System;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace PmcReader.TestApp
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            var plugin = new PmcReaderSensorPlugin();
            var updateInterval = new BehaviorSubject<TimeSpan>(TimeSpan.FromSeconds(1));

            await plugin.InitializeAsync(updateInterval);

            var subscription = plugin.SensorSnapshotStream.Subscribe(snapshot =>
            {
                Console.Clear();
                Console.WriteLine($"{plugin.Name} @ {snapshot.Item1:O}");
                foreach (var pair in snapshot.Item2.OrderBy(p => p.Key.Name))
                {
                    Console.WriteLine($"{pair.Key.Name}: {pair.Value}");
                }
            });

            Console.WriteLine("Press any key to stop...");
            Console.ReadKey(true);
            subscription.Dispose();
        }
    }
}
