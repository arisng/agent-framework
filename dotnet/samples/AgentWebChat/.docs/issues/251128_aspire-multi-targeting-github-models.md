# Aspire Multi-Targeting & GitHub Models Configuration

**Date:** 2025-11-28
**Issue Type:** Technical Issue
**Severity:** Medium
**Status:** Resolved

## 📋 Summary

When configuring the AgentWebChat sample to use free GitHub Models instead of Azure OpenAI, we encountered a multi-framework targeting issue where the parent `Directory.Build.props` injected `net472` alongside `net10.0`, causing Aspire orchestration failures. The solution involved creating a local `Directory.Build.props` override and properly configuring the Azure AI Inference endpoint for GitHub Models.

## 🔍 Analysis / Context

### Problem 1: GitHub Models Configuration

- The original sample was configured for Azure OpenAI, requiring an Azure subscription
- GitHub Models provides free access to GPT-4o and other models via Azure AI Inference-compatible endpoint
- Endpoint: `https://models.inference.ai.azure.com`

### Problem 2: Multi-Framework Targeting Conflict

- Parent `samples/Directory.Build.props` defined: `<TargetFrameworks>net10.0;net472</TargetFrameworks>`
- Aspire packages (v13.0.0) don't support `net472`
- Running projects standalone or via Aspire resulted in framework resolution errors:

  ```
  Your project targets multiple frameworks. Specify which framework to run using '--framework'.
  ```

- NuGet restore errors:

  ```
  Package Aspire.Hosting.Azure.CognitiveServices 13.0.0 is not compatible with net472
  ```

### Problem 3: Aspire Connection String Injection

- Running `AgentHost` directly (not via `AppHost`) resulted in empty connection strings
- The `ChatClientExtensions` expects connection string format: `Endpoint=...;AccessKey=...;Model=...;Provider=...`
- Aspire injects these only when orchestrating through the AppHost

## ✅ Resolution / Decision

### 1. GitHub Models Configuration

Updated `AppHost/Program.cs` to use Azure AI Inference with GitHub token:

```csharp
var githubToken = builder.AddParameter("GitHubToken", secret: true);
var chatModel = builder.AddAIModel("chat-model")
    .AsAzureAIInference(
        modelName: "gpt-4o",  // or "gpt-4o-mini", "Meta-Llama-3.1-405B-Instruct"
        endpoint: "https://models.inference.ai.azure.com",
        apiKey: githubToken);
```

### 2. Framework Override via Local Directory.Build.props

Created `AgentWebChat/Directory.Build.props`:

```xml
<Project>
  <Import Project="../Directory.Build.props" />
  <PropertyGroup>
    <TargetFrameworks>net10.0</TargetFrameworks>
  </PropertyGroup>
</Project>
```

### 3. Secret Configuration

Added GitHub token to `AppHost/appsettings.Development.json`:

```json
{
  "Parameters": {
    "GitHubToken": "your_github_token_here"
  }
}
```

## 📚 Lessons Learned

- **Aspire requires AppHost orchestration**: Individual projects cannot run standalone when they depend on Aspire-injected connection strings
- **Directory.Build.props inheritance**: Child projects inherit from parent `Directory.Build.props`; override locally when needed
- **`TargetFrameworks` vs `TargetFramework`**: Using plural form signals multi-targeting to MSBuild, even with one value
- **GitHub Models compatibility**: GitHub Models uses Azure AI Inference SDK, not OpenAI SDK directly
- **Aspire doesn't expose framework selection API**: There's no `AddProject<T>().WithTargetFramework()` - framework is determined by project files

## 🛠️ Prevention / Implementation

### For New Aspire Samples in Multi-Targeted Repos:

1. Create a local `Directory.Build.props` that overrides `<TargetFrameworks>` to the single required TFM
2. Always run via the AppHost project, not individual service projects
3. Use `dotnet run --project path/to/AppHost.csproj` without `--framework` flag

### GitHub Token Setup:

1. Go to https://github.com/settings/tokens
2. Generate a classic token (no special scopes needed)
3. Add to `appsettings.Development.json` under `Parameters.GitHubToken`

### Available Free Models on GitHub Models:

| Model          | Model Name                     |
| -------------- | ------------------------------ |
| GPT-4o         | `gpt-4o`                       |
| GPT-4o mini    | `gpt-4o-mini`                  |
| Llama 3.1 405B | `Meta-Llama-3.1-405B-Instruct` |
| DeepSeek R1    | `DeepSeek-R1`                  |

## 🔗 Related Files

- [`AgentWebChat/Directory.Build.props`](../Directory.Build.props) - Framework override
- [`AgentWebChat.AppHost/Program.cs`](../../AgentWebChat.AppHost/Program.cs) - GitHub Models configuration
- [`AgentWebChat.AppHost/appsettings.Development.json`](../../AgentWebChat.AppHost/appsettings.Development.json) - Secret configuration
- [`AgentWebChat.AgentHost/Utilities/ChatClientExtensions.cs`](../../AgentWebChat.AgentHost/Utilities/ChatClientExtensions.cs) - Connection string parsing
- [`samples/Directory.Build.props`](../../../Directory.Build.props) - Parent multi-targeting config (line 7)

## 📖 Additional Resources

- [GitHub Models Marketplace](https://github.com/marketplace/models)
- [Azure AI Inference SDK](https://learn.microsoft.com/en-us/dotnet/api/azure.ai.inference)
- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [MSBuild Directory.Build.props](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory)

## 🏷️ Tags

`aspire` `dotnet` `github-models` `azure-ai-inference` `multi-targeting` `configuration` `local-development` `msbuild` `directory-build-props` `medium-priority`
