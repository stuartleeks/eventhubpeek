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
using EventHubPeek.Utils;

namespace EventHubPeek.Controllers
{
    public class EventHubController : Controller
    {
        private const string APPSETTING_PREFIX = "APPSETTING_";

        [HttpGet]
        [Route("")]
        public ActionResult Index()
        {
            var connections = GetConnectionsFromAppSettingsEnvironmentVariables();
            var model = new IndexModel
            {
                ConsumerGroupName = "$Default", // was $Default
                MaximumMessages = 100,
                StartDateTimeUtc = DateTime.UtcNow.AddMinutes(-5),
                MaximumWaitTime = "0:0:10",
                ConnectionsInSettings = ProjectSelectList(connections, null),
                SelectedConnectionInSettings = null
            };
            return View(model);
        }

        [HttpPost]
        [Route("")]
        public async Task<ActionResult> Index(IndexModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    model.EventHubMessages = await GetMessagesAsync(model);
                }
                catch(Exception ex)
                {
                    model.OutputMessage = ex.Message;
                }
            }
            var connections = GetConnectionsFromAppSettingsEnvironmentVariables();
            model.ConnectionsInSettings = ProjectSelectList(connections, model.SelectedConnectionInSettings);

            return View(model);
        }

        private async Task<IEnumerable<MessageInfo>> GetMessagesAsync(IndexModel model)
        {
     
            string connectionString = model.InputConnectionStrings.LastOrDefault(s=>!string.IsNullOrWhiteSpace(s)); // will get dropdown value then textbox value. Take textbox value as a preference
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
            while (messages.Count < maximumMessages && ((maxWaitTime = waitEndTimeUtc - DateTime.UtcNow) > TimeSpan.Zero))
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


        private static List<DefinedConnection> GetConnectionsFromAppSettingsEnvironmentVariables()
        {
            return Environment.GetEnvironmentVariables()
                    .Cast<System.Collections.DictionaryEntry>()
                    .Where(de => IsAnAppSetting(de) && LooksLikeAnEventHubConnectionString((string)de.Value))
                    .Select(de =>
                        new DefinedConnection
                        {
                            Name = ((string)de.Key).Substring(APPSETTING_PREFIX.Length),
                            ConnectionString = ((string)de.Value)
                        }
                    )
                    .ToList();
        }
        private static List<SelectListItem> ProjectSelectList(IEnumerable<DefinedConnection> connections, string selectedConnectionString)
        {
            return connections.Select(c => new SelectListItem
            {
                Text = c.Name + "(" + c.ConnectionString + ")",
                Value = c.ConnectionString,
                Selected = c.ConnectionString == selectedConnectionString
            })
            .Prepend( new SelectListItem())
            .ToList();
        }

        private static bool IsAnAppSetting(System.Collections.DictionaryEntry de)
        {
            return ((string)de.Key).StartsWith(APPSETTING_PREFIX, StringComparison.InvariantCultureIgnoreCase);
        }

        private static bool LooksLikeAnEventHubConnectionString (string value)
        {
            if (value.IndexOf("sb://", StringComparison.Ordinal) < 0)
            {
                return false;
            }
            try
            {
                new ServiceBusConnectionStringBuilder(value);
                return true;
            }
            catch
            {
                return false;
            }
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