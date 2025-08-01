﻿using Blazor.Api.Models.Cosmos;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Blazor.Api.Services;

public class CosmosDbService : ICosmosDbService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Container _usersContainer;
    private readonly Container _placesContainer;

    public CosmosDbService(IOptions<CosmosDbSettings> options)
    {
        return;
        
        var settings = options.Value;

        // Use CosmosClientBuilder to configure the client instead of CosmosJsonDotNetSerializer
        var cosmosClientBuilder = new CosmosClientBuilder(settings.Account, settings.Key)
            .WithSerializerOptions(new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
            });

        _cosmosClient = cosmosClientBuilder.Build();
        
        var database = _cosmosClient.GetDatabase(settings.DatabaseName);

        _usersContainer = database.CreateContainerIfNotExistsAsync(
            "Users",
            "/partitionKey",
            400).Result;
        
        _placesContainer = database.CreateContainerIfNotExistsAsync(
            "Places",
            "/partitionKey",
            2000).Result;
    }

    public async Task<ItemResponse<T>> AddItemAsync<T>(T item, string partitionKey) where T : EntityBase
    {
        var response = await _usersContainer.CreateItemAsync(
            item,
            new PartitionKey(partitionKey));

        Debug.WriteLine(response.StatusCode);

        return response;
    }

    public async Task AddItemsAsync<T>(IEnumerable<T> items) where T : EntityBase
    {
        var tasks = items.Select(item => _usersContainer.CreateItemAsync(item, new PartitionKey(item.PartitionKey)));
        await Task.WhenAll(tasks);
    }


    public async Task<T?> GetItemAsync<T>(string id, string partitionKey) where T : class
    {
        try
        {
            var response = await _usersContainer.ReadItemAsync<T>(id, new PartitionKey(partitionKey));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IEnumerable<T>> GetItemsAsync<T>(string query) where T : class
    {
        var queryDef = new QueryDefinition(query);
        var resultSet = _usersContainer.GetItemQueryIterator<T>(queryDef);
        List<T> results = new();

        while (resultSet.HasMoreResults)
        {
            var response = await resultSet.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public async Task<ItemResponse<T>> AddPlaceAsync<T>(T item, string partitionKey) where T : EntityBase
    {
        var response = await _placesContainer.CreateItemAsync(item, new PartitionKey(partitionKey));
        Debug.WriteLine(response.StatusCode);
        
        return response;
    }
}