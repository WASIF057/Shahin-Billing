using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using ShahinBilling.Api.Models;

namespace ShahinBilling.Api.Data;

public class MongoSettings
{
    public string ConnectionString { get; set; } = "";
    public string Database { get; set; } = "shahin_billing";
}

public static class MongoConfig
{
    private static bool _registered;

    /// <summary>camelCase fields, enums as strings, money as Decimal128. Call once at startup.</summary>
    public static void Register()
    {
        if (_registered) return;
        _registered = true;

        ConventionRegistry.Register("shahin", new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreExtraElementsConvention(true),
            new EnumRepresentationConvention(BsonType.String)
        }, _ => true);

        BsonSerializer.RegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
        BsonSerializer.RegisterSerializer(new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));
        BsonSerializer.RegisterSerializer(new DateTimeSerializer(DateTimeKind.Utc));
    }
}

public class MongoContext
{
    public IMongoDatabase Db { get; }

    public MongoContext(IOptions<MongoSettings> options)
    {
        var s = options.Value;
        if (string.IsNullOrWhiteSpace(s.ConnectionString))
            throw new InvalidOperationException(
                "Mongo:ConnectionString is not set. Copy appsettings.example.json to appsettings.Development.json and fill it in.");
        Db = new MongoClient(s.ConnectionString).GetDatabase(s.Database);
    }

    public IMongoCollection<User> Users => Db.GetCollection<User>("users");
    public IMongoCollection<Business> Businesses => Db.GetCollection<Business>("businesses");
    public IMongoCollection<Item> Items => Db.GetCollection<Item>("items");
    public IMongoCollection<ProductType> ProductTypes => Db.GetCollection<ProductType>("productTypes");
    public IMongoCollection<EmailTemplate> EmailTemplates => Db.GetCollection<EmailTemplate>("emailTemplates");
    public IMongoCollection<BillFormat> BillFormats => Db.GetCollection<BillFormat>("billFormats");
    public IMongoCollection<LoginChallenge> LoginChallenges => Db.GetCollection<LoginChallenge>("loginChallenges");
    public IMongoCollection<ActivityEntry> Activity => Db.GetCollection<ActivityEntry>("activity");
    public IMongoCollection<Order> Orders => Db.GetCollection<Order>("orders");
    public IMongoCollection<Client> Clients => Db.GetCollection<Client>("clients");
    public IMongoCollection<Invoice> Invoices => Db.GetCollection<Invoice>("invoices");
    public IMongoCollection<Counter> Counters => Db.GetCollection<Counter>("counters");

    public async Task EnsureIndexesAsync()
    {
        await Users.Indexes.CreateOneAsync(new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(x => x.Email), new CreateIndexOptions { Unique = true }));

        await Items.Indexes.CreateOneAsync(new CreateIndexModel<Item>(
            Builders<Item>.IndexKeys.Ascending(x => x.BusinessId).Ascending(x => x.Name)));

        await ProductTypes.Indexes.CreateOneAsync(new CreateIndexModel<ProductType>(
            Builders<ProductType>.IndexKeys.Ascending(x => x.BusinessId).Ascending(x => x.Name)));

        await EmailTemplates.Indexes.CreateOneAsync(new CreateIndexModel<EmailTemplate>(
            Builders<EmailTemplate>.IndexKeys.Ascending(x => x.BusinessId).Ascending(x => x.Name)));

        await BillFormats.Indexes.CreateOneAsync(new CreateIndexModel<BillFormat>(
            Builders<BillFormat>.IndexKeys.Ascending(x => x.BusinessId).Ascending(x => x.Name)));

        await LoginChallenges.Indexes.CreateOneAsync(new CreateIndexModel<LoginChallenge>(
            Builders<LoginChallenge>.IndexKeys.Ascending(x => x.ExpiresAt), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }));

        await Activity.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ActivityEntry>(Builders<ActivityEntry>.IndexKeys.Ascending(x => x.BusinessId).Descending(x => x.At)),
            new CreateIndexModel<ActivityEntry>(Builders<ActivityEntry>.IndexKeys.Ascending(x => x.At), new CreateIndexOptions { ExpireAfter = TimeSpan.FromDays(365) }),
        });

        await Orders.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Order>(Builders<Order>.IndexKeys.Ascending(x => x.BusinessId).Descending(x => x.CreatedAt)),
            new CreateIndexModel<Order>(Builders<Order>.IndexKeys.Ascending(x => x.BusinessId).Ascending(x => x.ClientId)),
            new CreateIndexModel<Order>(Builders<Order>.IndexKeys.Ascending(x => x.BusinessId).Ascending(x => x.Status)),
        });

        await Clients.Indexes.CreateOneAsync(new CreateIndexModel<Client>(
            Builders<Client>.IndexKeys.Ascending(x => x.BusinessId).Ascending(x => x.Name)));

        await Invoices.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Invoice>(
                Builders<Invoice>.IndexKeys.Ascending(x => x.BusinessId).Ascending(x => x.InvoiceNumber),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<Invoice>(
                Builders<Invoice>.IndexKeys.Ascending(x => x.BusinessId).Descending(x => x.InvoiceDate)),
            new CreateIndexModel<Invoice>(
                Builders<Invoice>.IndexKeys.Ascending(x => x.BusinessId).Ascending(x => x.ClientId))
        });
    }
}
