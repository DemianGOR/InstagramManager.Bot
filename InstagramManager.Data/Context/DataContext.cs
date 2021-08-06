using InstagramManager.Data.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace InstagramManager.Data.Context
{
    public sealed class DataContext
    {
        public readonly IMongoCollection<InstagramTask> Tasks;
        public readonly IMongoCollection<Person> Users;
        public readonly IMongoCollection<Product> Products;
        public readonly IMongoCollection<PassedTask> PassedTasks;
        public readonly IMongoCollection<Promocodes> Promocodes;
        public readonly IMongoCollection<Admin> Admin;

        public DataContext(IOptions<DataOptions> options)
        {
            var mongoClient = new MongoClient(options.Value.ConnectionString);
            var database = mongoClient.GetDatabase(options.Value.DatabaseName);

            Tasks = database.GetCollection<InstagramTask>(nameof(Tasks));
            Users = database.GetCollection<Person>(nameof(Users));
            Products = database.GetCollection<Product>(nameof(Products));
            PassedTasks = database.GetCollection<PassedTask>(nameof(PassedTasks));
            Promocodes = database.GetCollection<Promocodes>(nameof(Promocodes));
            Admin = database.GetCollection<Admin>(nameof(Admin));
        }
    }
}
