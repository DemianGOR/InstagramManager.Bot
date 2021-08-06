using InstagramManager.Data.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InstagramManager.Data.Models
{
    public class InstagramTask
    {
        public ObjectId Id { get; set; }

        public int OwnerId { get; set; }

        public int TotalCrowns { get; set; }

        [BsonRepresentation(BsonType.Int32)]
        public InstagramTaskMode Mode { get; set; }

        [BsonIgnoreIfNull]
        public string MediaId { get; set; }

        [BsonIgnoreIfNull]
        public string Url { get; set; }
    }
}
