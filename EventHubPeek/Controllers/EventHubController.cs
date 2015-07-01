using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using EventHubPeek.Models.EventHub;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace EventHubPeek.Controllers
{
    public class EventHubController : Controller
    {
        [HttpGet]
        [Route("")]
        public ActionResult Index()
        {
            var model = new IndexModel
            {
                ConsumerGroupName = "$Default",
                MaximumMessages = 100,
                StartDateTimeUtc = DateTime.UtcNow.AddMinutes(-15),
                MaximumWaitTime = "0:0:10"
            };
            return View(model);
        }

        [HttpPost]
        [Route("")]
        public async Task<ActionResult> Index(IndexModel model)
        {
            if (ModelState.IsValid)
            {
                model.Messages = await GetMessagesAsync(model);
            }
            return View(model);
        }

        private async Task<IEnumerable<MessageInfo>> GetMessagesAsync(IndexModel model)
        {
            string connectionString = model.ConnectionString;
            string consumerGroupName = model.ConsumerGroupName;
            DateTime startingTimeUtc = model.StartDateTimeUtc;
            int maxMessages = model.MaximumMessages;
            TimeSpan maxWaitTime = TimeSpan.Parse(model.MaximumWaitTime);


            var builder = new ServiceBusConnectionStringBuilder(connectionString);

            var hubName = builder.EntityPath;

            var client = EventHubClient.CreateFromConnectionString(connectionString);
            var consumerGroup = client.GetConsumerGroup(consumerGroupName);


            DateTime waitEndTime = DateTime.UtcNow + maxWaitTime;


            var receivers = await client.GetRuntimeInformation()
                    .PartitionIds
                    .Select(p => consumerGroup.CreateReceiverAsync(p, startingTimeUtc))
                    .WaitAllAndUnwrap();

            var receiveBatches = await receivers
                    .Select(async r => new { PartitionId = r.PartitionId, Messages = await ReceiveAsync(r, maxMessages, waitEndTime) })
                    .WaitAllAndUnwrap();

            var messages = receiveBatches
                    .SelectMany(b => b.Messages.Select(m => new { PartitionId = b.PartitionId, Message = m }))
                    .ToList()
                    .OrderBy(m => m.Message.EnqueuedTimeUtc)
                    .Take(maxMessages)
                    .Select(m => ExpandMessage(m.PartitionId, m.Message))
                    .ToList()
                    ;

            await receivers
                .Select(r => r.CloseAsync())
                .WaitAll();

            return messages;
        }

        private static async Task<List<EventData>> ReceiveAsync(EventHubReceiver receiver, int maximumMessages, DateTime waitEndTimeUtc)
        {
            var messages = new List<EventData>(capacity: maximumMessages);
            TimeSpan maxWaitTime;
            while (messages.Count < maximumMessages && ((maxWaitTime =  waitEndTimeUtc - DateTime.UtcNow) > TimeSpan.Zero))
            {
                // TODO - base wait time on the time spent so far
                var batch = await receiver.ReceiveAsync(maximumMessages - messages.Count, maxWaitTime);
                if (batch == null)
                {
                    return messages;
                }
                messages.AddRange(batch);
            }
            return messages;
        }


        private static MessageInfo ExpandMessage(string partitionId, EventData eventData)
        {

            string body;
            using (var stream = eventData.GetBodyStream())
            using (var reader = new StreamReader(stream))
            {
                body = reader.ReadToEnd();
            }

            return new MessageInfo
            {
                PartitionId = partitionId,
                EnqueuedTimeUtc = eventData.EnqueuedTimeUtc,
                Body = body
            };
        }


    }


    public static class TaskLinqExtensions
    {
        public static IEnumerable<T> BlockAndWaitAll<T>(this IEnumerable<T> source)
            where T : Task
        {
            var sourceList = source.ToList();
            Task.WhenAll(sourceList).Wait();
            return sourceList;
        }
        public static IEnumerable<T> UnwrapResults<T>(this IEnumerable<Task<T>> source)
        {
            return source.Select(t => t.Result);
        }
        public static async Task<IEnumerable<T>> WaitAllAndUnwrap<T>(this IEnumerable<Task<T>> source)
        {
            var sourceList = source.ToList();
            await Task.WhenAll(sourceList);
            return sourceList.UnwrapResults();
        }
        public static async Task WaitAll(this IEnumerable<Task> source)
        {
            var sourceList = source.ToList();
            await Task.WhenAll(sourceList);
        }
    }
}