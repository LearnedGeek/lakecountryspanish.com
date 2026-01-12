using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Models.ViewModels;

namespace LakeCountrySpanish.Web.Services;

public class ClaudeApiService : IClaudeApiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ClaudeApiService> _logger;
    private readonly string? _apiKey;
    private readonly string _model;

    private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";

    public ClaudeApiService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<ClaudeApiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _apiKey = configuration["Claude:ApiKey"];
        _model = configuration["Claude:Model"] ?? "claude-3-5-sonnet-20241022";
    }

    public async Task<bool> IsAvailableAsync()
    {
        return !string.IsNullOrEmpty(_apiKey);
    }

    public async Task<GeneratedAssignment> GenerateAssignmentAsync(AssignmentGenerationRequest request)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            return new GeneratedAssignment
            {
                Success = false,
                ErrorMessage = "Claude API key is not configured"
            };
        }

        try
        {
            var prompt = BuildAssignmentGenerationPrompt(request);

            var response = await CallClaudeApiAsync(prompt);

            if (response == null)
            {
                return new GeneratedAssignment
                {
                    Success = false,
                    ErrorMessage = "Failed to get response from Claude API"
                };
            }

            var result = ParseAssignmentResponse(response, request);
            result.GenerationPrompt = prompt;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating assignment via Claude API");
            return new GeneratedAssignment
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<GradingResult> GradeResponseAsync(GradingRequest request)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            // Fallback to simple string matching if API not available
            return SimpleGrade(request);
        }

        try
        {
            var prompt = BuildGradingPrompt(request);
            var response = await CallClaudeApiAsync(prompt);

            if (response == null)
            {
                return SimpleGrade(request);
            }

            return ParseGradingResponse(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error grading response via Claude API");
            return SimpleGrade(request);
        }
    }

    private string BuildAssignmentGenerationPrompt(AssignmentGenerationRequest request)
    {
        var typeDescription = request.Type switch
        {
            AssignmentType.FillInTheBlank => "fill-in-the-blank exercises where students complete sentences",
            AssignmentType.MultipleChoice => "multiple choice questions with 4 options each",
            AssignmentType.Translation => "translation exercises (Spanish to English and English to Spanish)",
            AssignmentType.Matching => "matching exercises pairing items from two columns",
            AssignmentType.ShortAnswer => "short answer questions requiring brief written responses",
            AssignmentType.Reading => "reading comprehension with a short passage and questions",
            AssignmentType.Mixed => "a mix of different exercise types",
            _ => "language learning exercises"
        };

        var sb = new StringBuilder();
        sb.AppendLine("You are a Spanish language teaching assistant. Generate a Spanish language assignment.");
        sb.AppendLine();
        sb.AppendLine($"CEFR Level: {request.CefrLevel}");
        sb.AppendLine($"Exercise Type: {typeDescription}");
        sb.AppendLine($"Number of Questions: {request.QuestionCount}");

        if (!string.IsNullOrEmpty(request.TopicName))
        {
            sb.AppendLine($"Topic: {request.TopicName}");
        }

        if (!string.IsNullOrEmpty(request.TopicDescription))
        {
            sb.AppendLine($"Topic Description: {request.TopicDescription}");
        }

        if (!string.IsNullOrEmpty(request.Keywords))
        {
            sb.AppendLine($"Keywords to include: {request.Keywords}");
        }

        if (!string.IsNullOrEmpty(request.AdditionalInstructions))
        {
            sb.AppendLine($"Additional Instructions: {request.AdditionalInstructions}");
        }

        sb.AppendLine();
        sb.AppendLine("Respond with a JSON object in this exact format:");
        sb.AppendLine(@"{
  ""title"": ""Assignment title"",
  ""description"": ""Brief instructions for the student"",
  ""questions"": [
    {
      ""id"": 1,
      ""type"": ""fill_blank"",
      ""question"": ""The question text with _____ for blanks"",
      ""options"": [""option1"", ""option2"", ""option3"", ""option4""],
      ""hint"": ""Optional hint""
    }
  ],
  ""answers"": [
    {
      ""id"": 1,
      ""answer"": ""correct answer"",
      ""acceptableAnswers"": [""alternative1"", ""alternative2""],
      ""explanation"": ""Why this is correct""
    }
  ],
  ""estimatedMinutes"": 10,
  ""totalPoints"": 10
}");
        sb.AppendLine();
        sb.AppendLine("For multiple choice, include the 'options' array. For fill-in-the-blank, use _____ to mark blanks.");
        sb.AppendLine("Ensure content is appropriate for the specified CEFR level.");
        sb.AppendLine("Return ONLY the JSON object, no additional text.");

        return sb.ToString();
    }

    private string BuildGradingPrompt(GradingRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a Spanish language teacher grading a student's answer.");
        sb.AppendLine();
        sb.AppendLine($"CEFR Level: {request.CefrLevel}");
        sb.AppendLine($"Question: {request.Question}");
        sb.AppendLine($"Expected Answer: {request.CorrectAnswer}");
        sb.AppendLine($"Student's Answer: {request.StudentAnswer}");

        if (!string.IsNullOrEmpty(request.AcceptableVariations))
        {
            sb.AppendLine($"Acceptable Variations: {request.AcceptableVariations}");
        }

        sb.AppendLine();
        sb.AppendLine("Evaluate the student's answer considering:");
        sb.AppendLine("- Correctness of meaning");
        sb.AppendLine("- Grammar accuracy");
        sb.AppendLine("- Spelling (minor typos may be acceptable)");
        sb.AppendLine("- Acceptable synonyms or alternative phrasings");
        sb.AppendLine();
        sb.AppendLine("Respond with a JSON object:");
        sb.AppendLine(@"{
  ""isCorrect"": true/false,
  ""score"": 0.0 to 1.0,
  ""feedback"": ""Brief feedback for the student"",
  ""explanation"": ""Why the answer is correct/incorrect"",
  ""suggestedImprovement"": ""How to improve (if applicable)""
}");
        sb.AppendLine();
        sb.AppendLine("Return ONLY the JSON object.");

        return sb.ToString();
    }

    private async Task<string?> CallClaudeApiAsync(string prompt)
    {
        var requestBody = new
        {
            model = _model,
            max_tokens = 4096,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", AnthropicVersion);

        var response = await _httpClient.PostAsync(AnthropicApiUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Claude API error: {StatusCode} - {Error}", response.StatusCode, error);
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);

        // Extract the text content from Claude's response
        var contentArray = doc.RootElement.GetProperty("content");
        if (contentArray.GetArrayLength() > 0)
        {
            var firstContent = contentArray[0];
            if (firstContent.TryGetProperty("text", out var textElement))
            {
                return textElement.GetString();
            }
        }

        return null;
    }

    private GeneratedAssignment ParseAssignmentResponse(string response, AssignmentGenerationRequest request)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                var questions = root.TryGetProperty("questions", out var q) ? q.GetRawText() : "[]";
                var answers = root.TryGetProperty("answers", out var a) ? a.GetRawText() : "[]";

                return new GeneratedAssignment
                {
                    Title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "Spanish Exercise" : "Spanish Exercise",
                    Description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                    QuestionsJson = questions,
                    AnswersJson = answers,
                    TotalPoints = root.TryGetProperty("totalPoints", out var tp) ? tp.GetInt32() : request.QuestionCount * 2,
                    EstimatedMinutes = root.TryGetProperty("estimatedMinutes", out var em) ? em.GetInt32() : 10,
                    Success = true
                };
            }

            return new GeneratedAssignment
            {
                Success = false,
                ErrorMessage = "Could not parse JSON from response"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing assignment response");
            return new GeneratedAssignment
            {
                Success = false,
                ErrorMessage = $"Parse error: {ex.Message}"
            };
        }
    }

    private GradingResult ParseGradingResponse(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                return new GradingResult
                {
                    IsCorrect = root.TryGetProperty("isCorrect", out var ic) && ic.GetBoolean(),
                    Score = root.TryGetProperty("score", out var s) ? s.GetDecimal() : 0,
                    Feedback = root.TryGetProperty("feedback", out var f) ? f.GetString() ?? "" : "",
                    Explanation = root.TryGetProperty("explanation", out var e) ? e.GetString() : null,
                    SuggestedImprovement = root.TryGetProperty("suggestedImprovement", out var si) ? si.GetString() : null
                };
            }

            return SimpleGrade(new GradingRequest());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing grading response");
            return SimpleGrade(new GradingRequest());
        }
    }

    private static GradingResult SimpleGrade(GradingRequest request)
    {
        // Simple string comparison fallback
        var isCorrect = string.Equals(
            request.StudentAnswer?.Trim(),
            request.CorrectAnswer?.Trim(),
            StringComparison.OrdinalIgnoreCase);

        return new GradingResult
        {
            IsCorrect = isCorrect,
            Score = isCorrect ? 1.0m : 0.0m,
            Feedback = isCorrect ? "Correct!" : "That's not quite right.",
            Explanation = isCorrect ? null : $"The expected answer was: {request.CorrectAnswer}"
        };
    }

    public async Task<PlacementQuestion> GeneratePlacementQuestionAsync(
        string cefrLevel,
        List<string>? previousTopics = null)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            return new PlacementQuestion
            {
                Success = false,
                ErrorMessage = "Claude API key is not configured"
            };
        }

        try
        {
            var prompt = BuildPlacementQuestionPrompt(cefrLevel, previousTopics);
            var response = await CallClaudeApiAsync(prompt);

            if (response == null)
            {
                return new PlacementQuestion
                {
                    Success = false,
                    ErrorMessage = "Failed to get response from Claude API"
                };
            }

            return ParsePlacementQuestionResponse(response, cefrLevel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating placement question via Claude API");
            return new PlacementQuestion
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private string BuildPlacementQuestionPrompt(string cefrLevel, List<string>? previousTopics)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a Spanish language placement test generator. Generate a single question to assess a student's Spanish proficiency.");
        sb.AppendLine();
        sb.AppendLine($"CEFR Level to assess: {cefrLevel}");
        sb.AppendLine();

        // Add level-specific guidance
        sb.AppendLine("Level guidelines:");
        sb.AppendLine(cefrLevel switch
        {
            "A1" => "- Basic vocabulary (greetings, numbers, colors, family)\n- Present tense of ser/estar\n- Simple sentence structure",
            "A2" => "- Everyday vocabulary (food, travel, shopping)\n- Present tense regular/irregular verbs\n- Basic past tense (preterite)",
            "B1" => "- Expanded vocabulary and expressions\n- Imperfect vs preterite\n- Subjunctive basics\n- Object pronouns",
            "B2" => "- Idiomatic expressions\n- Complex subjunctive uses\n- Conditional sentences\n- Advanced verb tenses",
            "C1" => "- Nuanced vocabulary\n- All subjunctive tenses\n- Complex grammatical structures\n- Literary/formal language",
            "C2" => "- Native-level expressions and idioms\n- Subtle grammatical distinctions\n- Regional variations\n- Professional/academic vocabulary",
            _ => "- Appropriate for intermediate level"
        });
        sb.AppendLine();

        if (previousTopics?.Any() == true)
        {
            sb.AppendLine($"IMPORTANT: Avoid these topics (already tested): {string.Join(", ", previousTopics)}");
            sb.AppendLine("Choose a different grammar point or vocabulary theme.");
            sb.AppendLine();
        }

        sb.AppendLine("Generate ONE question. Vary the question type (prefer multiple choice for reliability).");
        sb.AppendLine();
        sb.AppendLine("Respond with a JSON object in this exact format:");
        sb.AppendLine(@"{
  ""type"": ""multiple_choice"",
  ""question"": ""Complete: Yo _____ español. (I speak Spanish)"",
  ""options"": [""hablo"", ""hablas"", ""habla"", ""hablan""],
  ""correctAnswer"": ""hablo"",
  ""acceptableAnswers"": [],
  ""hint"": ""Think about the first person singular"",
  ""explanation"": ""Hablo is the first person singular form of hablar"",
  ""topic"": ""present tense verbs""
}");
        sb.AppendLine();
        sb.AppendLine("For fill-in-the-blank, use type 'fill_blank' and omit 'options'.");
        sb.AppendLine("Return ONLY the JSON object, no additional text.");

        return sb.ToString();
    }

    private PlacementQuestion ParsePlacementQuestionResponse(string response, string cefrLevel)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonString = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = JsonDocument.Parse(jsonString);
                var root = doc.RootElement;

                var question = new PlacementQuestion
                {
                    Level = cefrLevel,
                    Type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "multiple_choice" : "multiple_choice",
                    Question = root.TryGetProperty("question", out var q) ? q.GetString() ?? "" : "",
                    CorrectAnswer = root.TryGetProperty("correctAnswer", out var ca) ? ca.GetString() ?? "" : "",
                    Hint = root.TryGetProperty("hint", out var h) ? h.GetString() : null,
                    Explanation = root.TryGetProperty("explanation", out var e) ? e.GetString() : null,
                    Topic = root.TryGetProperty("topic", out var tp) ? tp.GetString() : null,
                    Success = true
                };

                // Parse options array
                if (root.TryGetProperty("options", out var options) && options.ValueKind == JsonValueKind.Array)
                {
                    question.Options = options.EnumerateArray()
                        .Select(o => o.GetString() ?? "")
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
                }

                // Parse acceptable answers array
                if (root.TryGetProperty("acceptableAnswers", out var acceptable) && acceptable.ValueKind == JsonValueKind.Array)
                {
                    question.AcceptableAnswers = acceptable.EnumerateArray()
                        .Select(o => o.GetString() ?? "")
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
                }

                return question;
            }

            return new PlacementQuestion
            {
                Success = false,
                ErrorMessage = "Could not parse JSON from response"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing placement question response");
            return new PlacementQuestion
            {
                Success = false,
                ErrorMessage = $"Parse error: {ex.Message}"
            };
        }
    }
}
