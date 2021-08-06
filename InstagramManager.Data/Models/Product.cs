using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Collections.Generic;

namespace InstagramManager.Data.Models
{
    public sealed class Product
    {
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public List<ProductItem> Items { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public bool IsActive { get; set; }
    }

    public sealed class ProductItem
    {
        public int VipDays { get; set; }

        public int Crowns { get; set; }

        public string Label { get; set; }

        public int Price { get; set; }
    }
}
