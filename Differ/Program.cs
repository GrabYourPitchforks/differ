using Differ;
using System.Threading.Channels;

ExecutionContext.SuppressFlow(); // we're a simple tool; don't bother flowing EC

Channel<DiffResult> channel = Channel.CreateUnbounded<DiffResult>();

var runner = new DiffRunner(
    left: new DirectoryInfo(args[0]),
    right: new DirectoryInfo(args[1]),
    writer: channel.Writer);

runner.Process();

await foreach (var item in channel.Reader.ReadAllAsync())
{
    Console.WriteLine($"{item.Path} >>> {item.Status}");
}

Console.WriteLine();
Console.WriteLine(">> COMPLETE <<");
