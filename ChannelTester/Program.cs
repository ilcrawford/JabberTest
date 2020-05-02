using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ChannelTester
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var joe = CreateMessenger("Joe", 5, 6);
            var ann = CreateMessenger("Ann", 10, 2);

            var ch = Merge(joe, ann);

            await foreach (var item in ch.ReadAllAsync())
                Console.WriteLine(item);
  
            
        }

        
        static ChannelReader<string> CreateMessenger(string msg, int count, int rndWait)
        {
            var ch = Channel.CreateUnbounded<string>();
            var rnd = new Random();

            Task.Run(async () =>
            {
                for (int i = 0; i < count; i++)
                {
                    await ch.Writer.WriteAsync($"{msg} {i}");
                    await Task.Delay(TimeSpan.FromSeconds(rnd.Next(rndWait)));
                }
                ch.Writer.Complete();
            });

            return ch.Reader;
        }

        static ChannelReader<T> Merge<T>(ChannelReader<T> first, ChannelReader<T> second)
        {
            var output = Channel.CreateUnbounded<T>();

            Task.Run(async () =>
            {
                await foreach (var item in first.ReadAllAsync())
                    await output.Writer.WriteAsync(item);
            });
            Task.Run(async () =>
            {
                await foreach (var item in second.ReadAllAsync())
                    await output.Writer.WriteAsync(item);
            });

            return output;
        }
    }
}
