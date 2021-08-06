using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InstagramManager.Data.Models
{
    public class PassedTask
    {
        public ObjectId Id { get; set; }

        public ObjectId PassedTaskId { get; set; }

        public int UserId { get; set; }

        [BsonDateTimeOptions(DateOnly = false, Kind = DateTimeKind.Utc)]
        public DateTime DateTime { get; set; }
    }
}
