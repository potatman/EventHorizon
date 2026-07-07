using System;
using System.Collections.Concurrent;
using Elastic.Clients.Elasticsearch.IndexManagement;
using EventHorizon.Abstractions.Interfaces;

namespace EventHorizon.EventStore.ElasticSearch.Models;

public class ElasticConfig
{
    public string[] Uris { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }

    private readonly ConcurrentDictionary<Type, Action<CreateIndexRequestDescriptor>> _indexOverrides = new();

    /// <summary>
    /// Escape hatch: full control over index creation for TState's snapshot/view indices.
    /// Runs after convention and StoreField-intent mapping, so anything set here wins.
    /// Only applies when the index is first created.
    /// </summary>
    public ElasticConfig ConfigureIndex<TState>(Action<CreateIndexRequestDescriptor> configure)
        where TState : class, IState
    {
        _indexOverrides[typeof(TState)] = configure;
        return this;
    }

    internal Action<CreateIndexRequestDescriptor> GetIndexOverride(Type stateType) =>
        _indexOverrides.TryGetValue(stateType, out var configure) ? configure : null;
}
