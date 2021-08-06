using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace InstagramManager.Data.Models
{
    public class Admin
    {
        [BsonId]
        public ObjectId Id { get; set; }
        public int IdTelegram { get; set; }
    }
}
