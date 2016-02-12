using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using EventHubPeek.Controllers;
using System.Web.Mvc;

namespace EventHubPeek.Models.EventHub
{
    public class IndexModel
    {
        public string[] InputConnectionStrings { get; set; }

        [Display(Name = "Custom Connection string")]
        public string ManualConnectionString { get; set; }

        [Display(Name= "Connection strings from config")]
        public List<SelectListItem> ConnectionsInSettings { get;  set; }

        public string SelectedConnectionInSettings { get; set; }

        [Display(Name = "Consumer group")]
        public string ConsumerGroupName { get; set; }

        [Display(Name = "Maximum # messages")]
        public int MaximumMessages { get; set; }

        [Display(Name = "Maximum wait time")]
        public string MaximumWaitTime { get; set; }

        [Display(Name = "Start time (UTC)")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd HH:mm:ss}", ApplyFormatInEditMode = true)]
        public DateTime StartDateTimeUtc { get; set; }

        public string OutputMessage { get; set; }

        public IEnumerable<MessageInfo> EventHubMessages { get; set; }

    }
    public class MessageInfo
    {
        public string Body { get; set; }
        public DateTime EnqueuedTimeUtc { get; set; }
        public string PartitionId { get; set; }
    }
    public class DefinedConnection
    {
        public string ConnectionString { get; set; }
        public string Name { get; set; }
    }

}