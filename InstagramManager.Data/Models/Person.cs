using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace InstagramManager.Data.Models
{
    public sealed class Person
    {
        [BsonIgnoreIfNull]
        public int InvitedBy { get; set; }
        public int Id { get; set; }

        public ObjectId ActiveTask { get; set; }

        [BsonIgnoreIfDefault]
        public int IsAwarded { get; set; }

        public string Status { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        [BsonIgnoreIfDefault]
        public bool RulesAccepted { get; set; }

        [BsonIgnoreIfDefault]
        public int Crowns { get; set; }

        [BsonIgnoreIfDefault]
        public DateTime VipTo { get; set; }

        [BsonIgnoreIfDefault]
        public bool FreeVipUsed { get; set; }

        [BsonIgnoreIfDefault]
        [BsonRepresentation(BsonType.Int32)]
        public int WatchStory { get; set; }

        [BsonIgnoreIfDefault]
        [BsonRepresentation(BsonType.Int32)]
        public int SaveToBookmarks { get; set; }

        [BsonIgnoreIfDefault]
        [BsonRepresentation(BsonType.Int32)]
        public int Subscribe { get; set; }

        [BsonIgnoreIfNull]
        public string MediaId { get; set; }

        public string Url { get; set; }

        [BsonIgnoreIfNull]
        public string LoginStatus { get; set; }
        [BsonIgnore]
        public string LoginData { get; set; }

       public bool IsBlocked { get; set; }
    }
}
