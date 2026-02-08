// Copyright (c) Microsoft. All rights reserved.

var builder = DistributedApplication.CreateBuilder(args);

// Register the AG-UI backend server with health monitoring.
// AGUIDojoServer exposes /health via app.MapHealthChecks("/health").
var server = builder.AddProject<Projects.AGUIDojoServer>("agui-server")
    .WithHttpHealthCheck("/health");

// Register the Blazor Server BFF client.
// - WithExternalHttpEndpoints: exposes the client URL in the Aspire dashboard
// - WithReference(server): enables Aspire service discovery from client to server
// - WaitFor(server): ensures the server is healthy before the client starts
// - WithEnvironment: injects the server URL so AGUIDojoClient's direct HttpClient
//   (SERVER_URL) and YARP reverse proxy cluster address both resolve to the
//   Aspire-managed server endpoint, without modifying AGUIDojoClient code.
var client = builder.AddProject<Projects.AGUIDojoClient>("agui-client")
    .WithExternalHttpEndpoints()
    .WithReference(server)
    .WaitFor(server)
    .WithEnvironment("SERVER_URL", server.GetEndpoint("http"))
    .WithEnvironment("ReverseProxy__Clusters__backend__Destinations__primary__Address", server.GetEndpoint("http"));

builder.Build().Run();
