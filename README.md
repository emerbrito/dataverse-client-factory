# EmBrito.Dataverse.Extensions.ClientFactory

[![Nuget](https://img.shields.io/nuget/v/EmBrito.Dataverse.Extensions.ClientFactory)](https://www.nuget.org/packages/EmBrito.Dataverse.Extensions.ClientFactory)
[![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/emerbrito/dataverse-client-factory/dotnet-build.yml)](https://github.com/emerbrito/dataverse-client-factory/actions/workflows/dotnet-build.yml)

## DataverseClientFactory and Connection Pooling

The `DataverseClientFactory` helps minimize the cost of openning or cloning Dataverse connections through connection pooling.

> In this article, the term "connection" refers to a connected [ServiceClient][1] instance (which can be used interchangeably anywhere an [IOrganizationService][5] or  [IOrganizationServiceAsync2][4] is used).

Whenever a user requests a new connection, the pooler looks for an available connection in the pool. If a pooled connection is available it returns it instead of opening a new one. When the application calls `Dispose` in the connection, the pooler returns it to the set of pooled connections instead of actually closing it. Once the connection is available in the pool it is ready to be used again.

``` mermaid
sequenceDiagram
    participant C as Consumer
    participant F as ClientFactory
    participant P as ConnectionPool
    C->>F: requests first connection
    F->>+C: clone internal client
    C-->>C: update records
    C->>-F: dispose() connection    
    F->>P: add connection to pool
    Note over C,P: connection is sent back to factory when Dispose() is called.
    C->>F: request new connection
    alt pool has available connections
    P-->>C: serve connection from pool
    else pool is empty 
    F->>+C: clone internal client
    end    
```

In the real world each application is different and the impact or performance gain will depend on the application however, any application can benefit from the layer of abstraction provided by the `DataverseClientFactory`.

## IDataverseClient

As previoulsy mentioned to benefit from the connection pool you must call `Dispose` in the opened connection however, it is common for applications to accept an [IOrganizationServiceAsync2][4] which doesn't implement it.

To provide the required methods the `DataverseClientFactory` returns an `IDataverseClient` instead. The only purpose of the custom interface is to provide the `Dispose` method.

This is the `IDataverseClient` source:

``` csharp
public interface IDataverseClient : IOrganizationServiceAsync2, IDisposable
{
}
```

With the exception of the `Dispose` method, when refactoring your code you should be able to replace any instances of `IDataverseClient` with `IOrganizationServiceAsync2` and vice versa.

## Using the DataverseClientFactory

Basic usage, NOT leveraging the connection pool:

``` csharp
DataverseClientFactory factory = new(myAppId, myAppSecret, myInstanceUrl, logger);
IOrganizationServiceAsync2 client = factory.CreateClient();

client.Create(someEntity);
```

Leveraging the connection pool by disposing of the client when it is no longer needed:

``` csharp
var factory = new DataverseClientFactory(myAppId, myAppSecret, myInstanceUrl, logger);

using (var client = factory.CreateClient())
{
    client.Create(someEntity);
}
``` 

> You should always dispose of the connection when it is no longer required so that the connection will be returned to the pool. You can do this by calling the `Dispose` method or by opening the connection inside a using statement. Connections that are not explicitely disposed are not returned to the connection pool.

## Dependency Injection

An extension method is provided to simplify the process of registering a `DataverseClientFactory`. 

``` csharp
public override void Configure(IFunctionsHostBuilder builder)
{

    builder.Services.AddOptions();
    builder.Services.AddLogging();
    builder.Services.AddDataverseClientFactory(options => 
    {
        options.ClientId = "clientId";
        options.ClientSecret = "clientSecret";
        options.DataverseInstanceUri = "clientUrl";

    });
}
```

And in your code:

``` csharp
public class MyCustomService
{
    readonly IDataverseClientFactory _factory;

    public MyCustomService(IDataverseClientFactory clientFactory)
    {
        _factory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    }

}
```

### Injecting an IDataverseClient

In some cases, depending on your application architecture you can inject an `IDataverseClient` instead of the factory and still leverage connection pooling since a container is responsible for the cleaning up of types it creates and will call Dispose on IDisposable instances.

Here is an example:

``` csharp
// startup.cs
public override void Configure(IFunctionsHostBuilder builder)
{

    builder.Services.AddOptions();
    builder.Services.AddLogging();
    builder.Services.AddDataverseClientFactory(options => 
    {
        options.ClientId = "clientId";
        options.ClientSecret = "clientSecret";
        options.DataverseInstanceUri = "clientUrl";

    });
    builder.Services.AddTransient<IDataverseClient>(provider =>
    {
        var factory = provider.GetRequiredService<DataverseClientFactory>();
        return factory.CreateDataverseClient();
    });    
}

// MyCustomService.cs
public class MyCustomService
{
    readonly IDataverseClient _client;

    public MyCustomService(IDataverseClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

}
```


## Connecting to Multiple Dataverse Instances

To handle connections to different dataverse instances you can create specialized factories that derives from `DataverseClientFactory` as demonstrated below.
These are meant to take in connection parameteres that are specific to each instance.

Creating two specialized factories:

``` csharp
public class ContosoClientFacotry : DataverseClientFactory
{
    public ContosoClientFacotry(ILogger<ContosoClientFacotry> logger) 
        : base("ContosoAppId", "ContosoAppSecret", "ContosoUrl", logger)
    {
    }
}

public class FabrikamClientFacotry : DataverseClientFactory
{
    public FabrikamClientFacotry(ILogger<FabrikamClientFacotry> logger)
        : base("FabrikamAppId", "FabrikamAppSecret", "FabrikamUrl", logger)
    {
    }
}
````

Using both factories:

``` csharp
var contosoFactory = new ContosoClientFacotry(contosoLogger);
var fabrikamFactory= new FabrikamClientFacotry(fabrikamLogger);

using (var contosoClient = contosoFactory.CreateClient())
using (var fabrikamClient = fabrikamFactory.CreateClient())
{
    var contact = contosoClient.Retrieve("contact", contactId);
    fabrikamClient.Create(contact);
}
```

## Under the Hood: IDataverseClient

The `IDataverseClient` is an instance of the [ServiceClient][1] wraped by a [DispatchProxy][3]. The [dispatchProxy][3] allow us to intercept and repurpose calls to `Dispose`.

## Benchmarks

These benchmarks where created using a Microsoft sample code that uses parallel tasks to increase throughput when connecting to a dataverse instance and performing data operations.

Because we want to focus on the time spent instantiating and serving connections, no data operations were performed.

The simulations used an IEnumerable containing 10, 100 and 500 records.
A maximun degree or parallelism of 8 was set, meaning at any giving time we could have 8 connections opened simultaneously.

In these particular tests you can see how we went from spending around 3.3 seconds cloning and serving 500 connections to 12 μs (microseconds) by using connection pooling. This would have saved us over 3 seconds in execution time if this was a real world application.

``` ini
BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.22621.963)
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.307
  [Host]     : .NET 6.0.12 (6.0.1222.56807), X64 RyuJIT AVX2
  DefaultJob : .NET 6.0.12 (6.0.1222.56807), X64 RyuJIT AVX2
```
| Method | TotalEntities | Mean | Error | StdDev | Rank | Gen0 | Gen1 | Allocated |
|----------------------------- |-------------- |-------------:|-----------:|------------:|-----:|---------:|---------:|-----------:|
| UseConnectionPool |   10 |     5.783 μs |  0.0295 μs |   0.0230 μs |    1 |   1.0300 |        - |    4.16 KB |
| UseConnectionPool |  100 |     7.445 μs |  0.0516 μs |   0.0457 μs |    2 |   1.2207 |        - |    4.96 KB |
| UseConnectionPool |  500 |    12.553 μs |  0.2470 μs |   0.4455 μs |    3 |   1.6174 |        - |     6.5 KB |
| CloneForEachThread |  100 | 3,330.354 μs | 58.0096 μs |  54.2622 μs |    4 | 292.9688 | 113.2813 | 1414.49 KB |
| CloneForEachThread |  500 | 3,339.151 μs | 38.1336 μs |  35.6702 μs |    4 | 289.0625 | 109.3750 | 1414.75 KB |
| CloneForEachThread |   10 | 3,519.453 μs | 67.8566 μs | 103.6242 μs |    5 | 292.9688 | 105.4688 | 1413.53 KB |


[1]: https://learn.microsoft.com/en-us/dotnet/api/microsoft.powerplatform.dataverse.client.serviceclient?view=dataverse-sdk-latest
[2]: https://learn.microsoft.com/en-us/dotnet/api/microsoft.powerplatform.dataverse.client.serviceclient.clone?view=dataverse-sdk-latest#microsoft-powerplatform-dataverse-client-serviceclient-clone
[3]: https://learn.microsoft.com/en-us/dotnet/api/system.reflection.dispatchproxy?view=net-6.0
[4]: https://learn.microsoft.com/en-us/dotnet/api/microsoft.powerplatform.dataverse.client.iorganizationserviceasync2?view=dataverse-sdk-latest
[5]: https://learn.microsoft.com/en-us/dotnet/api/microsoft.xrm.sdk.iorganizationservice?view=dataverse-sdk-latest
