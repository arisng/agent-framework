// Copyright (c) Microsoft. All rights reserved.

using AgentWebChat.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// GitHub Models - uses Azure AI Inference compatible endpoint
// Get your token from: https://github.com/settings/tokens (needs no special scopes for GitHub Models)
var githubToken = builder.AddParameter("GitHubToken", secret: true);
var chatModel = builder.AddAIModel("chat-model")
    .AsAzureAIInference(
        modelName: "gpt-5-mini",
        endpoint: "https://models.inference.ai.azure.com",
        apiKey: githubToken);

var agentHost = builder.AddProject<Projects.AgentWebChat_AgentHost>("agenthost")
    .WithHttpEndpoint(name: "devui")
    .WithUrlForEndpoint("devui", (url) => new() { Url = "/devui", DisplayText = "Dev UI" })
    .WithReference(chatModel);

builder.AddProject<Projects.AgentWebChat_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(agentHost)
    .WaitFor(agentHost);

builder.Build().Run();
