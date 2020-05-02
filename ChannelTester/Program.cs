using System;
using System.Collections.Generic;
using System.Linq;
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
            var jam = CreateMessenger("jam", 20, 1);

            var ch = Merge(joe, ann, jam);

            await foreach (var item in ch.ReadAllAsync())
                Console.WriteLine(item);

            jam = CreateMessenger("jam", 20, 1);
            var readers = Split<string>(jam, 3);
            var tasks = new List<Task>();
            for (int i = 0; i < readers.Count; i++)
            {
                var reader = readers[i];
                var index = i;
                tasks.Add(Task.Run(async () =>
                {
                    await foreach (var item in reader.ReadAllAsync())
                        Console.WriteLine($"Reader {index}: {item}");
                }));
            }

            await Task.WhenAll(tasks);



        }

        
        static ChannelReader<string> CreateMessenger(string msg, int count, int rndWait)
        {
            var ch = Channel.CreateUnbounded<string>();
            var rnd = new Random();

            Task.Run(async () =>
            {
                for (int i = 0; i < count; i++)
                {
                    Console.WriteLine("xxx");
                    await ch.Writer.WriteAsync($"{msg} {i}");
                    await Task.Delay(TimeSpan.FromSeconds(rnd.Next(rndWait)));
                }
                ch.Writer.Complete();
            });

            return ch.Reader;
        }

        static ChannelReader<T> Merge<T>(params ChannelReader<T>[] inputs)
        {
            var output = Channel.CreateUnbounded<T>();

            Task.Run(async () =>
            {
                async Task Redirect(ChannelReader<T> input)
                {
                    await foreach (var item in input.ReadAllAsync())
                        await output.Writer.WriteAsync(item);
                }

                await Task.WhenAll(inputs.Select(i => Redirect(i)).ToArray());
                output.Writer.Complete();
            });

            return output;
        }
        static IList<ChannelReader<T>> Split<T>(ChannelReader<T> ch, int n)
        {
            var outputs = new Channel<T>[n];

            for (int i = 0; i < n; i++)
                outputs[i] = Channel.CreateUnbounded<T>();

            Task.Run(async () =>
            {
                var index = 0;
                await foreach (var item in ch.ReadAllAsync())
                {
                    await outputs[index].Writer.WriteAsync(item);
                    index = (index + 1) % n;
                }

                foreach (var ch in outputs)
                    ch.Writer.Complete();
            });

            return outputs.Select(ch => ch.Reader).ToArray();
        }
    }
}
