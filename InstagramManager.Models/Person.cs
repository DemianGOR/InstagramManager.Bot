using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace InstagramManager.Data.Models
{
    public sealed class Person
    {
        public int Id { get; set; }

        [BsonIgnoreIfNull]
        public List<ObjectId> ActiveTasks { get; set; }

        public string Status { get; set; }

        public string Username { get; set; }

        public bool RulesAccepted { get; set; }
    }
}
