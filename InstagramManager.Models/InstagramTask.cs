using InstagramManager.Data.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InstagramManager.Data.Models
{
    public sealed class InstagramTask
    {
        public ObjectId Id { get; set; }

        public int OwnerId { get; set; }

        [BsonRepresentation(BsonType.Int32)]
        public InstagramTaskMode Mode { get; set; }

        public string Username { get; set; }
    }
}
