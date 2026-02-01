// Copyright (c) Microsoft. All rights reserved.

using System.Text;
using System.Text.Json;
using AGUIWebChatServer.Models;
using OpenAI.Chat;

namespace AGUIWebChatServer.Tools;

/// <summary>
/// Implements AI-powered quiz generation using LLM structured output.
/// </summary>
public sealed class QuizGeneratorTool : IQuizGeneratorTool
{
    private readonly ChatClient _chatClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<QuizGeneratorTool> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizGeneratorTool"/> class.
    /// </summary>
    /// <param name="chatClient">The chat client for LLM interaction.</param>
    /// <param name="jsonOptions">JSON serialization options.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    public QuizGeneratorTool(ChatClient chatClient, JsonSerializerOptions jsonOptions, ILogger<QuizGeneratorTool> logger)
    {
        this._chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        this._jsonOptions = jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions));
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<string> GenerateQuizAsync(QuizGenerationRequest request, CancellationToken cancellationToken = default)
    {
        this._logger.LogInformation("[QuizGeneratorTool] Starting quiz generation for topic: {Topic}, difficulty: {Difficulty}, questions: {Count}",
            request?.Topic, request?.Difficulty, request?.NumberOfQuestions);

        if (request == null)
        {
            this._logger.LogError("[QuizGeneratorTool] Request is null");
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Topic))
        {
            this._logger.LogError("[QuizGeneratorTool] Topic is empty");
            throw new ArgumentException("Topic cannot be empty.", nameof(request));
        }

        if (request.NumberOfQuestions <= 0)
        {
            this._logger.LogError("[QuizGeneratorTool] Invalid number of questions: {Count}", request.NumberOfQuestions);
            throw new ArgumentException("Number of questions must be positive.", nameof(request));
        }

        try
        {
            // Determine question types to generate
            string questionTypeInstructions = GetQuestionTypeInstructions(request.QuestionTypes, request.NumberOfQuestions);
            this._logger.LogDebug("[QuizGeneratorTool] Question type instructions: {Instructions}", questionTypeInstructions);

            // Build the prompt for quiz generation
            string prompt = BuildQuizPrompt(request, questionTypeInstructions);
            this._logger.LogDebug("[QuizGeneratorTool] Built prompt with {Length} characters", prompt.Length);

            // Create chat messages
            List<ChatMessage> messages =
            [
                new SystemChatMessage(GetSystemPrompt()),
                new UserChatMessage(prompt)
            ];

            // Configure chat options for structured JSON output
            ChatCompletionOptions chatOptions = new()
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "quiz_schema",
                    jsonSchema: BinaryData.FromString(GetQuizJsonSchema()),
                    jsonSchemaIsStrict: true)
            };

            this._logger.LogInformation("[QuizGeneratorTool] Calling LLM to generate quiz...");

            // Call the LLM to generate quiz
            ChatCompletion completion = await this._chatClient.CompleteChatAsync(
                messages,
                chatOptions,
                cancellationToken);

            this._logger.LogInformation("[QuizGeneratorTool] LLM response received. Finish reason: {Reason}", completion.FinishReason);

            // Extract the generated JSON
            string? generatedJson = completion.Content.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(generatedJson))
            {
                this._logger.LogError("[QuizGeneratorTool] LLM returned empty or null response");
                throw new InvalidOperationException("LLM returned empty response. Please ensure API credentials are properly configured (OPENAI_API_KEY or AZURE_OPENAI_ENDPOINT).");
            }

            this._logger.LogInformation("[QuizGeneratorTool] Received LLM response ({Length} chars). First 200 chars: {Preview}",
                generatedJson.Length,
                generatedJson.Length > 200 ? generatedJson.AsSpan(0, 200).ToString() : generatedJson);

            // Validate the generated JSON by deserializing and re-serializing
            try
            {
                JsonDocument.Parse(generatedJson); // Throws if invalid JSON
                this._logger.LogInformation("[QuizGeneratorTool] Quiz JSON validated successfully");
            }
            catch (JsonException jsonEx)
            {
                this._logger.LogError(jsonEx, "[QuizGeneratorTool] Generated response is not valid JSON. Response: {Response}",
                    generatedJson.Length > 500 ? generatedJson.AsSpan(0, 500).ToString() + "..." : generatedJson);
                throw new InvalidOperationException($"LLM returned invalid JSON: {jsonEx.Message}. This may indicate API authentication issues or model configuration problems.", jsonEx);
            }

            return generatedJson;
        }
        catch (Exception ex) when (ex is not ArgumentException and not ArgumentNullException and not InvalidOperationException)
        {
            // Log the full exception details
            this._logger.LogError(ex, "[QuizGeneratorTool] Unexpected error during quiz generation. Exception type: {Type}, Message: {Message}",
                ex.GetType().Name, ex.Message);

            // Check for common API authentication errors
            string errorMessage = "Failed to generate quiz. ";
            if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized") || ex.Message.Contains("authentication"))
            {
                errorMessage += "API authentication failed. Please verify that OPENAI_API_KEY or AZURE_OPENAI_ENDPOINT with proper credentials are configured.";
            }
            else if (ex.Message.Contains("403") || ex.Message.Contains("Forbidden"))
            {
                errorMessage += "API access forbidden. Please check your API key permissions and subscription status.";
            }
            else if (ex.Message.Contains("429") || ex.Message.Contains("rate limit"))
            {
                errorMessage += "API rate limit exceeded. Please try again later.";
            }
            else
            {
                errorMessage += $"Error: {ex.Message}. Please ensure API credentials (OPENAI_API_KEY or AZURE_OPENAI_ENDPOINT) are properly configured.";
            }

            throw new InvalidOperationException(errorMessage, ex);
        }
    }

    private static string GetSystemPrompt()
    {
        return """
            You are an expert quiz generator. Generate educational quizzes that are:
            - Accurate and factually correct
            - Clear and unambiguous
            - Appropriate for the specified difficulty level
            - Engaging and educational
            
            IMPORTANT: You must return ONLY valid JSON matching the provided schema, with NO additional text, markdown, or explanations.
            """;
    }

    private static string BuildQuizPrompt(QuizGenerationRequest request, string questionTypeInstructions)
    {
        StringBuilder prompt = new();
        prompt.AppendLine("Generate a quiz with the following requirements:");
        prompt.AppendLine($"- Topic: {request.Topic}");
        prompt.AppendLine($"- Difficulty: {request.Difficulty}");
        prompt.AppendLine($"- Number of questions: {request.NumberOfQuestions}");
        prompt.AppendLine(questionTypeInstructions);
        prompt.AppendLine();
        prompt.AppendLine("Quiz requirements:");
        prompt.AppendLine("- Create a descriptive title for the quiz");
        prompt.AppendLine("- Include clear instructions for the quiz taker");
        prompt.AppendLine("- Each question must have 3-5 answer options");
        prompt.AppendLine("- Single-select questions should have exactly ONE correct answer");
        prompt.AppendLine("- Multi-select questions should have 2-3 correct answers");
        prompt.AppendLine("- Incorrect answers should be plausible distractors");
        prompt.AppendLine("- Set correctAnswerDisplay.visibility to 'afterSubmit'");
        prompt.AppendLine("- Set userChoiceIds to empty array for all cards");
        prompt.AppendLine("- Do NOT include evaluation field (will be added after submission)");

        return prompt.ToString();
    }

    private static string GetQuestionTypeInstructions(List<string> questionTypes, int totalQuestions)
    {
        // If no types specified or "mixed" is included, generate a mix
        if (questionTypes == null || questionTypes.Count == 0 || questionTypes.Contains("mixed", StringComparer.OrdinalIgnoreCase))
        {
            int singleSelectCount = (int)Math.Ceiling(totalQuestions / 2.0);
            int multiSelectCount = totalQuestions - singleSelectCount;
            return $"- Question types: Generate {singleSelectCount} single-select and {multiSelectCount} multi-select questions";
        }

        // Count how many of each type to generate
        int singleSelect = questionTypes.Count(t => t.Equals("single-select", StringComparison.OrdinalIgnoreCase));
        int multiSelect = questionTypes.Count(t => t.Equals("multi-select", StringComparison.OrdinalIgnoreCase));

        if (singleSelect > 0 && multiSelect > 0)
        {
            return $"- Question types: Generate approximately {singleSelect} single-select and {multiSelect} multi-select questions";
        }
        else if (singleSelect > 0)
        {
            return "- Question types: All questions should be single-select (one correct answer)";
        }
        else if (multiSelect > 0)
        {
            return "- Question types: All questions should be multi-select (multiple correct answers)";
        }
        else
        {
            // Default to mixed if types are unrecognized
            int mixedSingle = (int)Math.Ceiling(totalQuestions / 2.0);
            int mixedMulti = totalQuestions - mixedSingle;
            return $"- Question types: Generate {mixedSingle} single-select and {mixedMulti} multi-select questions (default mix)";
        }
    }

    private static string GetQuizJsonSchema()
    {
        // JSON Schema for Quiz structure matching the data model
        // OpenAI structured output requires ALL properties to be listed in required array
        return """
        {
          "type": "object",
          "properties": {
            "id": { "type": "string" },
            "title": { "type": "string" },
            "instructions": { "type": "string" },
            "cards": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "id": { "type": "string" },
                  "sequence": { "type": "integer" },
                  "question": {
                    "type": "object",
                    "properties": {
                      "text": { "type": "string" },
                      "description": { "type": ["string", "null"] },
                      "mediaUrl": { "type": ["string", "null"] }
                    },
                    "required": ["text", "description", "mediaUrl"],
                    "additionalProperties": false
                  },
                  "answers": {
                    "type": "array",
                    "items": {
                      "type": "object",
                      "properties": {
                        "id": { "type": "string" },
                        "text": { "type": "string" },
                        "description": { "type": ["string", "null"] },
                        "mediaUrl": { "type": ["string", "null"] },
                        "isDisabled": { "type": "boolean" }
                      },
                      "required": ["id", "text", "description", "mediaUrl", "isDisabled"],
                      "additionalProperties": false
                    }
                  },
                  "selection": {
                    "type": "object",
                    "properties": {
                      "mode": { "type": "string", "enum": ["single", "multiple"] },
                      "minSelections": { "type": "integer" },
                      "maxSelections": { "type": "integer" }
                    },
                    "required": ["mode", "minSelections", "maxSelections"],
                    "additionalProperties": false
                  },
                  "correctAnswerIds": {
                    "type": "array",
                    "items": { "type": "string" }
                  },
                  "correctAnswerDisplay": {
                    "type": "object",
                    "properties": {
                      "visibility": { "type": "string", "enum": ["never", "afterSubmit", "afterReveal", "always"] },
                      "allowReveal": { "type": "boolean" }
                    },
                    "required": ["visibility", "allowReveal"],
                    "additionalProperties": false
                  },
                  "userChoiceIds": {
                    "type": "array",
                    "items": { "type": "string" }
                  }
                },
                "required": ["id", "sequence", "question", "answers", "selection", "correctAnswerIds", "correctAnswerDisplay", "userChoiceIds"],
                "additionalProperties": false
              }
            }
          },
          "required": ["id", "title", "instructions", "cards"],
          "additionalProperties": false
        }
        """;
    }
}
