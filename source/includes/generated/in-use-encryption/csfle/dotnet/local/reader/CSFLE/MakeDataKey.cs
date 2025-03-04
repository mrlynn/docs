﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.Encryption;

namespace Key
{

    class MakeDataKey
    {
        public static void MakeKey()
        {

            using (var randomNumberGenerator = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                var bytes = new byte[96];
                randomNumberGenerator.GetBytes(bytes);
                var localMasterKeyBase64Write = Convert.ToBase64String(bytes);
                Console.WriteLine(localMasterKeyBase64Write);
                File.WriteAllText("master-key.txt", localMasterKeyBase64Write);
            }

            // start-kmsproviders
            var kmsProviders = new Dictionary<string, IReadOnlyDictionary<string, object>>();
            var provider = "local";
            string localMasterKeyBase64Read = File.ReadAllText("master-key.txt");
            var localMasterKeyBytes = Convert.FromBase64String(localMasterKeyBase64Read);
            var localOptions = new Dictionary<string, object>
            {
                { "key", localMasterKeyBytes }
            };
            kmsProviders.Add("local", localOptions);
            // end-kmsproviders

            // start-datakeyopts
            // end-datakeyopts

            // start-create-index
            var connectionString = "<Your MongoDB URI>";
            // start-create-dek
            var keyVaultNamespace = CollectionNamespace.FromFullName("encryption.__keyVault");
            var keyVaultClient = new MongoClient(connectionString);
            var indexOptions = new CreateIndexOptions<BsonDocument>();
            indexOptions.Unique = true;
            indexOptions.PartialFilterExpression = new BsonDocument { { "keyAltNames", new BsonDocument { { "$exists", new BsonBoolean(true) } } } };
            var builder = Builders<BsonDocument>.IndexKeys;
            var indexKeysDocument = builder.Ascending("keyAltNames");
            var indexModel = new CreateIndexModel<BsonDocument>(indexKeysDocument, indexOptions);
            var keyVaultDatabase = keyVaultClient.GetDatabase(keyVaultNamespace.DatabaseNamespace.ToString());
            // Drop the Key Vault Collection in case you created this collection
            // in a previous run of this application.  
            keyVaultDatabase.DropCollection(keyVaultNamespace.CollectionName.ToString());
            // Drop the database storing your encrypted fields as all
            // the DEKs encrypting those fields were deleted in the preceding line.
            keyVaultClient.GetDatabase("medicalRecords").DropCollection("patients");
            var keyVaultCollection = keyVaultDatabase.GetCollection<BsonDocument>(keyVaultNamespace.CollectionName.ToString());
            keyVaultCollection.Indexes.CreateOne(indexModel);
            // end-create-index

            // start-create-dek
            var clientEncryptionOptions = new ClientEncryptionOptions(
                keyVaultClient: keyVaultClient,
                keyVaultNamespace: keyVaultNamespace,
                kmsProviders: kmsProviders);
            var clientEncryption = new ClientEncryption(clientEncryptionOptions);
            var dataKeyOptions = new DataKeyOptions();
            var dataKeyId = clientEncryption.CreateDataKey(provider, dataKeyOptions, CancellationToken.None);
            var dataKeyIdBase64 = Convert.ToBase64String(GuidConverter.ToBytes(dataKeyId, GuidRepresentation.Standard));
            Console.WriteLine($"DataKeyId [base64]: {dataKeyIdBase64}");
            // end-create-dek
        }
    }
}
