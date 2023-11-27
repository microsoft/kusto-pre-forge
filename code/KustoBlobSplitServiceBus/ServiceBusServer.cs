using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Kusto.Ingest.Exceptions;
using KustoBlobSplitLib;
using Microsoft.Identity.Client.AppConfig;
using System.Text.Json;

namespace KustoBlobSplitServiceBus
{
    public static class ServiceBusServer
    {
        public static async Task RunServerAsync(
            string serviceBusQueueUrl,
            RunningContext context)
        {
            var uri = new Uri(serviceBusQueueUrl, UriKind.Absolute);
            var queueName = uri
                .Segments
                .Where(s => s != "/")
                .FirstOrDefault();

            await using (var client = new ServiceBusClient(uri.Host, context.Credentials))
            await using (var receiver = client.CreateReceiver(queueName))
            {
                while (true)
                {
                    var message = await receiver.ReceiveMessageAsync(TimeSpan.FromMinutes(1));

                    if (message != null)
                    {
                        await ProcessOneMessageAsync(context, receiver, message);
                    }
                    else
                    {
                        Console.WriteLine("No blob detected");
                    }
                }
            }
        }

        private static async Task ProcessOneMessageAsync(
            RunningContext context,
            ServiceBusReceiver receiver,
            ServiceBusReceivedMessage message)
        {
            var payload = message.Body.ToObjectFromJson<Payload>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            var ctSource = new CancellationTokenSource();
            var renewTask = RecurrentlyRenewLockAsync(
                receiver,
                message,
                ctSource.Token);

            if (payload.Data == null
                || payload.Time == null
                || payload.Data.BlobUrl == null
                || !payload.Data.BlobUrl.IsAbsoluteUri)
            {
                throw new InvalidDataException(
                    "Queue payload invalid:  this isn't an Event Grid Cloud event");
            }

            Console.WriteLine();
            Console.WriteLine($"Queued blob:  {payload.Data?.BlobUrl}");
            Console.WriteLine($"Enqueued time:  {payload.Time}");

            var subSettings = context.OverrideSourceBlob(payload.Data?.BlobUrl!);

            await EtlRun.RunEtlAsync(subSettings);
            ctSource.Cancel();
            await renewTask;
            await receiver.CompleteMessageAsync(message);
        }

        private static async Task RecurrentlyRenewLockAsync(
            ServiceBusReceiver receiver,
            ServiceBusReceivedMessage message,
            CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(20), ct);
                    await receiver.RenewMessageLockAsync(message);
                }
            }
            catch (TaskCanceledException)
            {
            }
        }
    }
}