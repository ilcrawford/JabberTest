using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ChannelTester
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var dir = GetFilesRecursively("C:\\Users\\ilcra_000\\source\\repos\\JabberTest");
            var src = FilterByExtension(dir, new HashSet<string> { ".cs" });
            var cnt = GetLineCount(src);

            var total = 0;
            await foreach (var item in cnt.ReadAllAsync())
            {
                Console.WriteLine($"{item.file.FullName} {item.lines}");
                total += item.lines;
            }

            Console.WriteLine($"Total lines: {total}");
        }

        static ChannelReader<string> GetFilesRecursively(string root)
        {
            var output = Channel.CreateUnbounded<string>();

            async Task WalkDir(string path)
            {
                foreach (var file in Directory.GetFiles(path))
                    await output.Writer.WriteAsync(file);

                var tasks = Directory.GetDirectories(path).Select(WalkDir);
                await Task.WhenAll(tasks.ToArray());
            }

            Task.Run(async () =>
            {
                await WalkDir(root);
                output.Writer.Complete();
            });

            return output;
        }

        static ChannelReader<FileInfo> FilterByExtension(ChannelReader<string> input, HashSet<string> exts)
        {
            var output = Channel.CreateUnbounded<FileInfo>();

            Task.Run(async () =>
            {
                await foreach (var file in input.ReadAllAsync())
                {
                    var fileInfo = new FileInfo(file);
                    if (exts.Contains(fileInfo.Extension))
                        await output.Writer.WriteAsync(fileInfo);
                }
                output.Writer.Complete();
            });

            return output;
        }

        static ChannelReader<(FileInfo file, int lines)> GetLineCount(ChannelReader<FileInfo> input)
        {
            var output = Channel.CreateUnbounded<(FileInfo, int)>();

            Task.Run(async () =>
            {
                await foreach (var file in input.ReadAllAsync())
                {
                    var lines = CountLines(file);
                    await output.Writer.WriteAsync((file, lines));
                }
                output.Writer.Complete();
            });

            return output;
        }

        static int CountLines(FileInfo file)
        {
            using var sr = new StreamReader(file.FullName);
            var lines = 0;

            while (sr.ReadLine() != null)
                lines++;

            return lines;
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
