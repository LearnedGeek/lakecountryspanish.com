using System.Text;
using System.Text.Json;
using LakeCountrySpanish.Web.Models.Entities;
using LakeCountrySpanish.Web.Services.Curriculum.Blocks;

namespace LakeCountrySpanish.Web.Services.Curriculum.Drafter;

/// <summary>
/// Calls Anthropic Messages API with forced tool-use to produce a structured
/// lesson draft. See <c>docs/curriculum-system/ai-assisted-authoring.md</c>
/// for the architecture overview.
/// </summary>
public sealed class CurriculumDrafter : ICurriculumDrafter
{
    private const string AnthropicApiUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const string ToolName = "submit_lesson_draft";

    private readonly HttpClient _http;
    private readonly DraftingPromptBuilder _prompts;
    private readonly ILogger<CurriculumDrafter> _logger;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly int _maxTokens;

    public CurriculumDrafter(
        HttpClient http,
        IConfiguration configuration,
        DraftingPromptBuilder prompts,
        ILogger<CurriculumDrafter> logger)
    {
        _http = http;
        _prompts = prompts;
        _logger = logger;
        _apiKey = configuration["Claude:ApiKey"];
        // Drafter defaults to a newer model than IClaudeApiService since
        // structured-output / tool-use quality matters more here.
        _model = configuration["Claude:DrafterModel"] ?? "claude-sonnet-4-5";
        _maxTokens = configuration.GetValue<int?>("Claude:DrafterMaxTokens") ?? 8192;
    }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<DraftLessonResult> DraftAsync(DraftLessonRequest request, CancellationToken ct = default)
    {
        if (!IsAvailable)
        {
            return new DraftLessonResult
            {
                Success = false,
                ErrorMessage = "Claude API key is not configured. Set Claude:ApiKey in appsettings.Local.json."
            };
        }

        if (string.IsNullOrWhiteSpace(request.Brief))
        {
            return new DraftLessonResult
            {
                Success = false,
                ErrorMessage = "Brief is empty. Describe the lesson you want to draft."
            };
        }

        var prompt = await _prompts.BuildAsync(request, ct);

        try
        {
            using var schemaDoc = JsonDocument.Parse(prompt.ToolInputSchemaJson);
            var requestBody = new
            {
                model = _model,
                max_tokens = _maxTokens,
                system = prompt.SystemPrompt,
                tools = new[]
                {
                    new
                    {
                        name = ToolName,
                        description = "Submit a structured Lake Country Spanish lesson draft.",
                        input_schema = schemaDoc.RootElement
                    }
                },
                tool_choice = new { type = "tool", name = ToolName },
                messages = new[]
                {
                    new { role = "user", content = prompt.UserMessage }
                }
            };

            using var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, AnthropicApiUrl) { Content = content };
            requestMessage.Headers.Add("x-api-key", _apiKey);
            requestMessage.Headers.Add("anthropic-version", AnthropicVersion);

            using var response = await _http.SendAsync(requestMessage, ct);
            var bodyText = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Anthropic draft request failed: {Status} {Body}", response.StatusCode, bodyText);
                return new DraftLessonResult
                {
                    Success = false,
                    ErrorMessage = $"Anthropic returned {(int)response.StatusCode}. Check the server log for details.",
                    SystemPrompt = prompt.SystemPrompt
                };
            }

            return ParseToolUseResponse(bodyText, prompt.SystemPrompt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error drafting lesson for brief: {Brief}", request.Brief);
            return new DraftLessonResult
            {
                Success = false,
                ErrorMessage = "Drafting failed: " + ex.Message,
                SystemPrompt = prompt.SystemPrompt
            };
        }
    }

    private DraftLessonResult ParseToolUseResponse(string responseBody, string systemPrompt)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var contentArray = doc.RootElement.GetProperty("content");

        JsonElement? toolUseInput = null;
        foreach (var block in contentArray.EnumerateArray())
        {
            if (block.TryGetProperty("type", out var typeProp) &&
                typeProp.GetString() == "tool_use" &&
                block.TryGetProperty("name", out var nameProp) &&
                nameProp.GetString() == ToolName &&
                block.TryGetProperty("input", out var input))
            {
                toolUseInput = input;
                break;
            }
        }

        if (toolUseInput is null)
        {
            _logger.LogWarning("Anthropic response missing expected tool_use block: {Body}", responseBody);
            return new DraftLessonResult
            {
                Success = false,
                ErrorMessage = "Anthropic did not call the structured-output tool. Try rephrasing the brief.",
                SystemPrompt = systemPrompt
            };
        }

        var input2 = toolUseInput.Value;
        var rawJson = input2.GetRawText();

        try
        {
            var title    = input2.GetProperty("title").GetString() ?? string.Empty;
            var theme    = input2.GetProperty("theme").GetString() ?? string.Empty;
            var unitId   = input2.GetProperty("unitId").GetInt32();
            var gradeStr = input2.GetProperty("gradeBand").GetString() ?? "K4";
            var duration = input2.GetProperty("estimatedDurationMinutes").GetInt32();

            var standards = new List<string>();
            if (input2.TryGetProperty("wiStandardCodes", out var stdArray))
            {
                foreach (var code in stdArray.EnumerateArray())
                {
                    var s = code.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) standards.Add(s);
                }
            }

            var blocks = new List<Block>();
            foreach (var b in input2.GetProperty("blocks").EnumerateArray())
            {
                var blockType = b.GetProperty("type").GetString();
                Block? parsed = blockType switch
                {
                    "paragraph" => ParseParagraph(b),
                    "vocab"     => ParseVocab(b),
                    "raw"       => ParseRaw(b),
                    _           => null
                };
                if (parsed is not null) blocks.Add(parsed);
            }

            return new DraftLessonResult
            {
                Success = true,
                Title = title,
                Theme = theme,
                UnitId = unitId,
                GradeBand = Enum.TryParse<GradeBand>(gradeStr, out var gb) ? gb : GradeBand.K4,
                EstimatedDurationMinutes = duration,
                WiStandardCodes = standards,
                Blocks = blocks,
                SystemPrompt = systemPrompt,
                RawToolInputJson = rawJson
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to map drafter tool input to DraftLessonResult. Raw: {Raw}", rawJson);
            return new DraftLessonResult
            {
                Success = false,
                ErrorMessage = "Drafter returned malformed structured output: " + ex.Message,
                SystemPrompt = systemPrompt,
                RawToolInputJson = rawJson
            };
        }
    }

    private static ParagraphBlock ParseParagraph(JsonElement b) => new()
    {
        Text = b.TryGetProperty("text", out var t) ? (t.GetString() ?? string.Empty) : string.Empty,
        ContainerKind = b.TryGetProperty("containerKind", out var c) && c.ValueKind == JsonValueKind.String
            ? c.GetString()
            : null,
        Time = b.TryGetProperty("time", out var tm) && tm.ValueKind == JsonValueKind.String
            ? tm.GetString()
            : null
    };

    private static VocabBlock ParseVocab(JsonElement b)
    {
        var rows = new List<VocabRow>();
        if (b.TryGetProperty("rows", out var rowsArray))
        {
            foreach (var r in rowsArray.EnumerateArray())
            {
                rows.Add(new VocabRow
                {
                    Spanish       = r.TryGetProperty("spanish", out var sp) ? sp.GetString() ?? "" : "",
                    English       = r.TryGetProperty("english", out var en) ? en.GetString() ?? "" : "",
                    Pronunciation = r.TryGetProperty("pronunciation", out var pr) ? pr.GetString() ?? "" : "",
                    Cue           = r.TryGetProperty("cue", out var cu) ? cu.GetString() ?? "" : ""
                });
            }
        }
        return new VocabBlock { Rows = rows };
    }

    private static RawMarkdownBlock ParseRaw(JsonElement b) => new()
    {
        Markdown = b.TryGetProperty("markdown", out var m) ? (m.GetString() ?? string.Empty) : string.Empty
    };
}
