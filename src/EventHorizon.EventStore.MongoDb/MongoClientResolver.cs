using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Reflection;
using EventHorizon.EventStore.MongoDb.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EventHorizon.EventStore.MongoDb
{
    public class MongoClientResolver : IClientResolver<MongoClient>
    {
        private readonly IOptions<MongoConfig> _options;

        public MongoClientResolver(IOptions<MongoConfig> options)
        {
            _options = options;
        }

        public MongoClient GetClient()
        {
            // https://www.mongodb.com/docs/drivers/csharp/current/fundamentals/connection/connection-options/
            var clientSettings = new MongoClientSettings()
            {
                UseTls = true,
                ApplicationName = AssemblyUtil.AssemblyName,
                Credential = new MongoCredential("MONGODB-AWS",
                    new MongoExternalIdentity("<awsKeyId>"),
                    new PasswordEvidence("<awsSecretKey>"))
            };

            // https://www.mongodb.com/docs/mongoid/master/reference/collection-configuration/
            var collectionSettings = new MongoCollectionSettings
            {
                ReadConcern = ReadConcern.Linearizable,
                ReadPreference = ReadPreference.PrimaryPreferred,
                WriteConcern = WriteConcern.W1
            };

            // Question:
            // - Attribute cant use Class Models... discuss

            // var mongoClientSettings = MongoClientSettings.FromConnectionString("");


            return new MongoClient(MongoUrl.Create(_options.Value.ConnectionString));
        }
    }
}
