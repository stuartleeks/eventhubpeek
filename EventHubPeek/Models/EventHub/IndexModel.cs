using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using EventHubPeek.Controllers;

namespace EventHubPeek.Models.EventHub
{
    public class IndexModel
    {
        [Display(Name = "Connection string")]
        public string ConnectionString { get; set; }
        [Display(Name = "Consumer group")]
        public string ConsumerGroupName { get; set; }
        [Display(Name = "Maximum # messages")]
        public int MaximumMessages { get; set; }
        [Display(Name = "Maximum wait time")]
        public string MaximumWaitTime { get; set; }
        [Display(Name = "Start time (UTC)")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime StartDateTimeUtc { get; set; }
        public IEnumerable<MessageInfo> Messages { get; set; }
    }
    public class MessageInfo
    {
        public string Body { get; internal set; }
        public DateTime EnqueuedTimeUtc { get; internal set; }
        public string PartitionId { get; internal set; }
    }
}