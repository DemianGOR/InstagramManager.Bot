using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InstagramManager.Data.Models
{
    public class Promocodes
    {
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        [BsonIgnoreIfDefault]
        public bool IsUsed { get; set; }
        [BsonIgnoreIfDefault]
        public int UsedBy { get; set; }
        public int Cash { get; set; }
       // public string Url { get; set; }
    }
}
