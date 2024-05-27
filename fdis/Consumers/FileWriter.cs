﻿using System.Threading.Channels;
using fdis.Data;
using fdis.Interfaces;

namespace fdis.Consumers
{
    public class FileWriter : IConsumer
    {
        public async ValueTask<Result> Consume(ContentInfo contentInfos, CancellationToken cancellationToken = default)
        {
            var saveFolder = Program.Configuration["SaveFolder"] ?? Directory.GetCurrentDirectory();
            Console.WriteLine($"Consuming {contentInfos}");
            if (!File.Exists(contentInfos.Path))
                return Result.Error($"{contentInfos.Path} doesn't exist");

            var data = await File.ReadAllBytesAsync(contentInfos.Path, cancellationToken);
            var savePath = Path.Combine(saveFolder, contentInfos.FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            await File.WriteAllBytesAsync(savePath, data, cancellationToken);

            return Result.Success($"{contentInfos.Path} copied to {savePath}");
        }

        public async ValueTask<List<Result>> ConsumeData(Channel<ContentInfo> contentChannel, CancellationToken cancellationToken = default)
        {
            var results = new List<Result>();
            await foreach (var contentInfo in contentChannel.Reader.ReadAllAsync(cancellationToken))
            {
                var result = await Consume(contentInfo, cancellationToken);
                results.Add(result);
            }

            return results;
        }
    }
}
