using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TaskManager.Tests;

public sealed class SmtpServer : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new(TimeSpan.FromSeconds(10));
    private readonly Task processing;
    public TaskCompletionSource<string> Message { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    public SmtpServer()
    {
        listener.Start();
        processing = ProcessAsync();
    }

    private async Task ProcessAsync()
    {
        try
        {
            using var connection = await listener.AcceptTcpClientAsync(stop.Token);
            await using var stream = connection.GetStream();
            using var reader = new StreamReader(stream);
            await using var writer = new StreamWriter(stream) { AutoFlush = true, NewLine = "\r\n" };
            await writer.WriteLineAsync("220 localhost SMTP");
            while (!stop.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(stop.Token);
                if (line is null) break;
                if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase))
                    await writer.WriteLineAsync("250 localhost");
                else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("354 End with a dot");
                    var body = new StringBuilder();
                    while (await reader.ReadLineAsync(stop.Token) is { } content && content != ".")
                        body.AppendLine(content.StartsWith("..", StringComparison.Ordinal) ? content[1..] : content);
                    Message.TrySetResult(body.ToString());
                    await writer.WriteLineAsync("250 Accepted");
                }
                else if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("221 Bye");
                    break;
                }
                else await writer.WriteLineAsync("250 OK");
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
        {
            Message.TrySetCanceled();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        listener.Stop();
        await processing;
        stop.Dispose();
    }
}
