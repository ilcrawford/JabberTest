using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ChannelTester
{
    class Program
    {
        static void Main(string[] args)
        {
            var ch = Channel.CreateUnbounded<string>();
            var consumer = Task.Run(async () =>
            {
                while (await ch.Reader.WaitToReadAsync())
                    Console.WriteLine(await ch.Reader.ReadAsync());
            });
            var producer = Task.Run(async () =>
            {
                var rnd = new Random();
                for (int i = 0; i < 5; i++)
                {
                    await Task.Delay(TimeSpan.FromSeconds(rnd.Next(3)));
                    await ch.Writer.WriteAsync($"Message {i}");
                }
                ch.Writer.Complete();
            });


            Task.WhenAll(producer, consumer).Wait();
            //Console.ReadLine();
        }
    }
}
