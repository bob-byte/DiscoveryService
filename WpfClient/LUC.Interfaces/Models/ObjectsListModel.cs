using System;
using System.Collections.Generic;

namespace LUC.Interfaces.Models
{
    public class ObjectsListModel
    {
        public ObjectsListModel()
        {
            ObjectDescriptions = new List<ObjectDescriptionModel>();
            DirectoryDescriptions = new List<DirectoryDescriptionModel>();
        }

        public List<ObjectDescriptionModel> ObjectDescriptions { get; set; }

        public List<DirectoryDescriptionModel> DirectoryDescriptions { get; set; }

        public DateTime ServerUtc { get; set; }

        public String RequestedPrefix { get; set; }
    }
}
