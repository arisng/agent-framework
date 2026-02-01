// Copyright (c) Microsoft. All rights reserved.

using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AGUIWebChat.Server.Mocks.ToolSets;

/// <summary>
/// Implements the quiz tool set that simulates quiz-related queries by emitting
/// <c>generate_quiz</c> or <c>get_quiz</c> tool calls based on context.
/// </summary>
/// <remarks>
/// <para>
/// This tool set demonstrates tool-based generative UI where the frontend can render
/// custom quiz components based on the tool call arguments. The sequence:
/// </para>
/// <list type="number">
/// <item><description>Optionally stream introductory text explaining the quiz generation</description></item>
/// <item><description>Emit <c>generate_quiz</c> with topic and question count arguments</description></item>
/// </list>
/// <para>
/// The tool arguments contain quiz metadata that enables the frontend to render an
/// interactive quiz card component with the questions and options.
/// </para>
/// </remarks>
public sealed class QuizToolSet : IToolSet
{
    /// <summary>
    /// Default topic when the user message doesn't specify one.
    /// </summary>
    private const string DefaultTopic = "General Knowledge";

    /// <summary>
    /// Default number of questions to generate.
    /// </summary>
    private const int DefaultQuestionCount = 5;

    /// <summary>
    /// Common topic keywords to extract from user messages.
    /// Maps common phrases to standardized topic names.
    /// </summary>
    private static readonly Dictionary<string, string> TopicKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["science"] = "Science",
        ["physics"] = "Physics",
        ["chemistry"] = "Chemistry",
        ["biology"] = "Biology",
        ["math"] = "Mathematics",
        ["mathematics"] = "Mathematics",
        ["history"] = "History",
        ["geography"] = "Geography",
        ["literature"] = "Literature",
        ["art"] = "Art History",
        ["music"] = "Music",
        ["sports"] = "Sports",
        ["technology"] = "Technology",
        ["programming"] = "Programming",
        ["coding"] = "Programming",
        ["movies"] = "Movies & Entertainment",
        ["film"] = "Movies & Entertainment",
        ["tv"] = "Television",
        ["food"] = "Food & Cuisine",
        ["animals"] = "Animals & Nature",
        ["nature"] = "Animals & Nature",
        ["space"] = "Space & Astronomy",
        ["astronomy"] = "Space & Astronomy"
    };

    /// <summary>
    /// The mock agent options containing delay configuration.
    /// </summary>
    private readonly MockAgentOptions _options;

    /// <summary>
    /// Whether to stream introductory text before the tool call.
    /// </summary>
    private readonly bool _includeIntroText;

    /// <summary>
    /// The introductory text template to stream before the tool call.
    /// </summary>
    private readonly string _introTextTemplate;

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizToolSet"/> class with default configuration.
    /// </summary>
    public QuizToolSet()
        : this(options: null, includeIntroText: true, introTextTemplate: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QuizToolSet"/> class with custom configuration.
    /// </summary>
    /// <param name="options">The mock agent options containing delay configuration. If <see langword="null"/>, default options are used.</param>
    /// <param name="includeIntroText">Whether to stream introductory text before the tool call. Defaults to <see langword="true"/>.</param>
    /// <param name="introTextTemplate">The introductory text template. Use {topic} and {count} placeholders. If <see langword="null"/>, uses a default template.</param>
    public QuizToolSet(MockAgentOptions? options = null, bool includeIntroText = true, string? introTextTemplate = null)
    {
        _options = options ?? new MockAgentOptions();
        _includeIntroText = includeIntroText;
        _introTextTemplate = introTextTemplate ?? "I'll create a quiz about {topic} with {count} questions. Let me generate that for you.";
    }

    /// <summary>
    /// Gets the unique name of this tool set.
    /// </summary>
    /// <value>Returns "QuizTools".</value>
    public string Name => "QuizTools";

    /// <summary>
    /// Gets the keywords or phrases that trigger this tool set.
    /// </summary>
    /// <value>A read-only list containing "quiz", "test me", "trivia", and "questions".</value>
    public IReadOnlyList<string> TriggerKeywords { get; } = new[] { "quiz", "test me", "trivia", "questions" };

    /// <summary>
    /// Executes the quiz tool sequence, emitting <c>generate_quiz</c> with topic and question count arguments.
    /// </summary>
    /// <param name="context">The execution context containing response metadata and helper methods.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>
    /// An asynchronous enumerable of <see cref="AgentResponseUpdate"/> instances containing
    /// optional introductory text followed by the <c>generate_quiz</c> tool call.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The sequence demonstrates tool-based generative UI:
    /// </para>
    /// <list type="number">
    /// <item><description>Optionally stream introductory text explaining the quiz generation</description></item>
    /// <item><description>Emit <c>generate_quiz</c> with topic (extracted from user message) and questionCount arguments</description></item>
    /// </list>
    /// <para>
    /// The frontend can use the tool arguments to render an interactive quiz component
    /// with the specified topic and number of questions.
    /// </para>
    /// </remarks>
    public async IAsyncEnumerable<AgentResponseUpdate> ExecuteSequenceAsync(
        MockSequenceContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Create logger for structured logging
        ILogger<QuizToolSet> logger = context.CreateLogger<QuizToolSet>();

        // Start overall timing
        Stopwatch overallStopwatch = Stopwatch.StartNew();

        // Extract topic and question count from user message
        string topic = ExtractTopic(context.UserMessage);
        int questionCount = ExtractQuestionCount(context.UserMessage);

        logger.LogInformation(
            "[QuizToolSet] Starting quiz sequence for topic: {Topic}, questions: {QuestionCount}, loading delay: {DelayMs}ms",
            topic,
            questionCount,
            _options.QuizLoadingDelayMs);

        // Optional: Stream introductory text before the tool call
        if (_includeIntroText)
        {
            string introText = _introTextTemplate
                .Replace("{topic}", topic)
                .Replace("{count}", questionCount.ToString());

            await foreach (AgentResponseUpdate update in context.StreamTextAsync(introText, cancellationToken).ConfigureAwait(false))
            {
                yield return update;
            }

            // Small delay between text and tool call for natural feel
            if (context.StreamingDelayMs > 0)
            {
                await Task.Delay(context.StreamingDelayMs * 2, cancellationToken).ConfigureAwait(false);
            }
        }

        // Emit generate_quiz tool call with topic and question count
        // Arguments include metadata for frontend quiz component rendering
        Dictionary<string, object?> generateQuizArgs = new()
        {
            ["topic"] = topic,
            ["questionCount"] = questionCount
        };

        // Start tool call timing
        Stopwatch toolStopwatch = Stopwatch.StartNew();
        logger.LogInformation(
            "[QuizToolSet] Emitting tool call: generate_quiz for topic {Topic} with {QuestionCount} questions",
            topic,
            questionCount);

        AgentResponseUpdate quizCallUpdate = context.CreateToolCallUpdate("generate_quiz", generateQuizArgs);
        string quizCallId = ((FunctionCallContent)quizCallUpdate.Contents[0]).CallId;
        yield return quizCallUpdate;

        // Apply loading delay to allow UI to show loading spinner before quiz renders
        if (_options.QuizLoadingDelayMs > 0)
        {
            logger.LogDebug(
                "[QuizToolSet] Waiting {DelayMs}ms to simulate quiz generation loading",
                _options.QuizLoadingDelayMs);
            await Task.Delay(_options.QuizLoadingDelayMs, cancellationToken).ConfigureAwait(false);
        }

        // Emit FunctionResultContent with quiz data matching expected schema
        Dictionary<string, object?> quizData = GenerateQuizData(topic, questionCount);
        yield return context.CreateToolResultUpdate(quizCallId, "generate_quiz", quizData);

        toolStopwatch.Stop();
        logger.LogInformation(
            "[QuizToolSet] Completed tool call: generate_quiz in {DurationMs}ms",
            toolStopwatch.ElapsedMilliseconds);

        overallStopwatch.Stop();
        logger.LogInformation(
            "[QuizToolSet] Quiz sequence completed in {DurationMs}ms",
            overallStopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Generates quiz data matching the expected Quiz schema with questions and answers.
    /// </summary>
    /// <param name="topic">The topic of the quiz.</param>
    /// <param name="questionCount">The number of questions to generate.</param>
    /// <returns>A dictionary representing the Quiz data structure.</returns>
    private static Dictionary<string, object?> GenerateQuizData(string topic, int questionCount)
    {
        string quizId = $"quiz_{Guid.NewGuid():N}";
        List<Dictionary<string, object?>> cards = GenerateQuestionCards(topic, questionCount);

        return new Dictionary<string, object?>
        {
            ["id"] = quizId,
            ["title"] = $"{topic} Quiz",
            ["instructions"] = $"Test your knowledge of {topic}! Answer all {questionCount} questions to complete the quiz.",
            ["cards"] = cards
        };
    }

    /// <summary>
    /// Generates question cards for the quiz based on the topic.
    /// </summary>
    /// <param name="topic">The topic of the quiz.</param>
    /// <param name="questionCount">The number of questions to generate.</param>
    /// <returns>A list of question card dictionaries.</returns>
    private static List<Dictionary<string, object?>> GenerateQuestionCards(string topic, int questionCount)
    {
        List<Dictionary<string, object?>> cards = new(questionCount);
        List<(string question, string[] answers, int correctIndex)> questionBank = GetQuestionsForTopic(topic);

        for (int i = 0; i < questionCount && i < questionBank.Count; i++)
        {
            (string question, string[] answers, int correctIndex) = questionBank[i];
            cards.Add(CreateQuestionCard(i, question, answers, correctIndex));
        }

        return cards;
    }

    /// <summary>
    /// Creates a single question card dictionary matching the QuestionCard schema.
    /// </summary>
    private static Dictionary<string, object?> CreateQuestionCard(
        int sequence,
        string questionText,
        string[] answerTexts,
        int correctAnswerIndex)
    {
        string cardId = $"card_{sequence + 1}";
        List<Dictionary<string, object?>> answers = new(answerTexts.Length);
        List<string> correctAnswerIds = new();

        for (int i = 0; i < answerTexts.Length; i++)
        {
            string answerId = $"{cardId}_opt_{i + 1}";
            answers.Add(new Dictionary<string, object?>
            {
                ["id"] = answerId,
                ["text"] = answerTexts[i],
                ["description"] = null,
                ["mediaUrl"] = null,
                ["isDisabled"] = false
            });

            if (i == correctAnswerIndex)
            {
                correctAnswerIds.Add(answerId);
            }
        }

        return new Dictionary<string, object?>
        {
            ["id"] = cardId,
            ["sequence"] = sequence,
            ["question"] = new Dictionary<string, object?>
            {
                ["text"] = questionText,
                ["description"] = null,
                ["mediaUrl"] = null
            },
            ["answers"] = answers,
            ["selection"] = new Dictionary<string, object?>
            {
                ["mode"] = "single",
                ["minSelections"] = 1,
                ["maxSelections"] = 1
            },
            ["correctAnswerIds"] = correctAnswerIds,
            ["correctAnswerDisplay"] = new Dictionary<string, object?>
            {
                ["visibility"] = "afterSubmit",
                ["allowReveal"] = false
            },
            ["userChoiceIds"] = new List<string>(),
            ["evaluation"] = null
        };
    }

    /// <summary>
    /// Gets a bank of questions for a specific topic.
    /// </summary>
    private static List<(string question, string[] answers, int correctIndex)> GetQuestionsForTopic(string topic)
    {
        return topic switch
        {
            "Science" => new(s_scienceQuestions),
            "Physics" => new(s_physicsQuestions),
            "Mathematics" => new(s_mathQuestions),
            "History" => new(s_historyQuestions),
            "Geography" => new(s_geographyQuestions),
            "Programming" => new(s_programmingQuestions),
            "Technology" => new(s_technologyQuestions),
            "Space & Astronomy" => new(s_spaceQuestions),
            _ => new(s_generalKnowledgeQuestions)
        };
    }

    #region Static Question Banks

    private static readonly (string, string[], int)[] s_generalKnowledgeQuestions =
    [
        ("What is the capital of France?", ["London", "Berlin", "Paris", "Madrid"], 2),
        ("Which planet is known as the Red Planet?", ["Venus", "Mars", "Jupiter", "Saturn"], 1),
        ("What is the largest ocean on Earth?", ["Atlantic", "Indian", "Arctic", "Pacific"], 3),
        ("How many continents are there?", ["5", "6", "7", "8"], 2),
        ("What is the chemical symbol for gold?", ["Go", "Gd", "Au", "Ag"], 2),
        ("Who painted the Mona Lisa?", ["Van Gogh", "Picasso", "Da Vinci", "Michelangelo"], 2),
        ("What is the tallest mountain in the world?", ["K2", "Kangchenjunga", "Mount Everest", "Lhotse"], 2)
    ];

    private static readonly (string, string[], int)[] s_scienceQuestions =
    [
        ("What is the chemical formula for water?", ["CO2", "H2O", "NaCl", "O2"], 1),
        ("What is the powerhouse of the cell?", ["Nucleus", "Ribosome", "Mitochondria", "Golgi body"], 2),
        ("What gas do plants absorb from the atmosphere?", ["Oxygen", "Nitrogen", "Carbon Dioxide", "Hydrogen"], 2),
        ("What is the speed of light in vacuum?", ["299,792 km/s", "150,000 km/s", "500,000 km/s", "1,000,000 km/s"], 0),
        ("What is the atomic number of Carbon?", ["8", "6", "12", "14"], 1)
    ];

    private static readonly (string, string[], int)[] s_physicsQuestions =
    [
        ("What is the SI unit of force?", ["Joule", "Watt", "Newton", "Pascal"], 2),
        ("What is Einstein's famous equation?", ["F=ma", "E=mc²", "PV=nRT", "V=IR"], 1),
        ("What particle has a negative charge?", ["Proton", "Neutron", "Electron", "Positron"], 2),
        ("What is the unit of electrical resistance?", ["Ampere", "Volt", "Ohm", "Watt"], 2),
        ("Which force keeps planets in orbit?", ["Electromagnetic", "Strong nuclear", "Weak nuclear", "Gravitational"], 3)
    ];

    private static readonly (string, string[], int)[] s_mathQuestions =
    [
        ("What is the value of Pi (π) to two decimal places?", ["3.12", "3.14", "3.16", "3.18"], 1),
        ("What is the square root of 144?", ["10", "11", "12", "13"], 2),
        ("What is 15% of 200?", ["25", "30", "35", "40"], 1),
        ("How many degrees are in a right angle?", ["45", "90", "180", "360"], 1),
        ("What is the next prime number after 7?", ["8", "9", "10", "11"], 3)
    ];

    private static readonly (string, string[], int)[] s_historyQuestions =
    [
        ("In what year did World War II end?", ["1943", "1944", "1945", "1946"], 2),
        ("Who was the first President of the United States?", ["Thomas Jefferson", "John Adams", "George Washington", "Benjamin Franklin"], 2),
        ("The Roman Empire fell in which century?", ["3rd Century", "4th Century", "5th Century", "6th Century"], 2),
        ("Which ancient wonder was located in Egypt?", ["Hanging Gardens", "Colossus of Rhodes", "Great Pyramid of Giza", "Lighthouse of Alexandria"], 2),
        ("The French Revolution began in which year?", ["1776", "1789", "1799", "1804"], 1)
    ];

    private static readonly (string, string[], int)[] s_geographyQuestions =
    [
        ("What is the longest river in the world?", ["Amazon", "Nile", "Yangtze", "Mississippi"], 1),
        ("What is the smallest country in the world?", ["Monaco", "San Marino", "Vatican City", "Liechtenstein"], 2),
        ("Which desert is the largest in the world?", ["Gobi", "Kalahari", "Sahara", "Antarctic"], 3),
        ("Mount Kilimanjaro is located in which country?", ["Kenya", "Tanzania", "Uganda", "Ethiopia"], 1),
        ("What is the capital of Australia?", ["Sydney", "Melbourne", "Canberra", "Brisbane"], 2)
    ];

    private static readonly (string, string[], int)[] s_programmingQuestions =
    [
        ("What does HTML stand for?", ["Hyper Text Markup Language", "High Tech Modern Language", "Home Tool Markup Language", "Hyperlink Text Management Language"], 0),
        ("Which symbol is used for comments in Python?", ["//", "/* */", "#", "--"], 2),
        ("What is the time complexity of binary search?", ["O(n)", "O(log n)", "O(n²)", "O(1)"], 1),
        ("Which language is primarily used for iOS development?", ["Java", "Kotlin", "Swift", "C#"], 2),
        ("What does API stand for?", ["Application Programming Interface", "Advanced Program Integration", "Automated Process Interface", "Application Process Integration"], 0)
    ];

    private static readonly (string, string[], int)[] s_technologyQuestions =
    [
        ("What does CPU stand for?", ["Central Processing Unit", "Computer Personal Unit", "Central Program Utility", "Core Processing Unit"], 0),
        ("Which company created the iPhone?", ["Google", "Samsung", "Apple", "Microsoft"], 2),
        ("What does RAM stand for?", ["Random Access Memory", "Read Access Memory", "Run Access Memory", "Real Access Memory"], 0),
        ("What is the name of Google's mobile operating system?", ["iOS", "Windows", "Android", "Linux"], 2),
        ("What protocol is used for secure web browsing?", ["HTTP", "FTP", "HTTPS", "SMTP"], 2)
    ];

    private static readonly (string, string[], int)[] s_spaceQuestions =
    [
        ("What is the closest star to Earth?", ["Proxima Centauri", "Alpha Centauri", "The Sun", "Sirius"], 2),
        ("How many planets are in our solar system?", ["7", "8", "9", "10"], 1),
        ("What is the largest planet in our solar system?", ["Saturn", "Neptune", "Jupiter", "Uranus"], 2),
        ("Who was the first person to walk on the Moon?", ["Buzz Aldrin", "Neil Armstrong", "Michael Collins", "Yuri Gagarin"], 1),
        ("What is the name of our galaxy?", ["Andromeda", "Milky Way", "Triangulum", "Sombrero"], 1)
    ];

    #endregion

    /// <summary>
    /// Extracts a topic from the user message by searching for known topic keywords.
    /// </summary>
    /// <param name="userMessage">The user's message to search for topic keywords.</param>
    /// <returns>The extracted topic, or <see cref="DefaultTopic"/> if no topic is found.</returns>
    private static string ExtractTopic(string? userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return DefaultTopic;
        }

        // Search for known topics in the user message
        foreach (KeyValuePair<string, string> topicEntry in TopicKeywords)
        {
            if (userMessage.Contains(topicEntry.Key, StringComparison.OrdinalIgnoreCase))
            {
                return topicEntry.Value;
            }
        }

        return DefaultTopic;
    }

    /// <summary>
    /// Extracts a question count from the user message by searching for numeric patterns.
    /// </summary>
    /// <param name="userMessage">The user's message to search for question count.</param>
    /// <returns>The extracted question count, or <see cref="DefaultQuestionCount"/> if none is found.</returns>
    private static int ExtractQuestionCount(string? userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return DefaultQuestionCount;
        }

        // Common patterns for question count: "5 questions", "10 question quiz", etc.
        string[] words = userMessage.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < words.Length - 1; i++)
        {
            if (int.TryParse(words[i], out int count) && count > 0 && count <= 20)
            {
                // Check if the next word is "question" or "questions"
                string nextWord = words[i + 1].TrimEnd('?', '!', ',', '.').ToUpperInvariant();
                if (nextWord is "QUESTION" or "QUESTIONS" or "Q")
                {
                    return count;
                }
            }
        }

        // Also check for word-based numbers
        if (userMessage.Contains("three", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (userMessage.Contains("five", StringComparison.OrdinalIgnoreCase))
        {
            return 5;
        }

        if (userMessage.Contains("ten", StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        return DefaultQuestionCount;
    }
}
