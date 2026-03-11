using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;

namespace Dataisland.LLM.Providers;

public class OpenAiProvider : ILlmProvider
{
    private readonly ChatClient _client;
    private readonly string _model;
    private readonly ILogger<OpenAiProvider> _logger;

    public OpenAiProvider(ModelConfig config, ILogger<OpenAiProvider> logger)
    {
        _logger = logger;
        _model = config.Model;

        var options = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(config.BaseUrl))
            options.Endpoint = new Uri(config.BaseUrl);

        var client = new OpenAIClient(new ApiKeyCredential(config.ApiKey!), options);
        _client = client.GetChatClient(config.Model);
    }

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
    {
        var options = BuildOptions(request);
        var messages = BuildMessages(request);

        var response = await _client.CompleteChatAsync(messages, options, ct);
        var value = response.Value;

        var text = value.Content.Count > 0 ? value.Content[0].Text : null;
        if (text is null)
        {
            _logger.LogWarning("OpenAI model {Model} returned no text content. FinishReason: {FinishReason}",
                _model, value.FinishReason);
            text = string.Empty;
        }

        return new LlmResponse(
            Content: text,
            PromptTokens: value.Usage?.InputTokenCount ?? 0,
            CompletionTokens: value.Usage?.OutputTokenCount ?? 0,
            Model: request.Model
        );
    }

    public async IAsyncEnumerable<string> CompleteStreamingAsync(
        LlmRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var options = BuildOptions(request);
        var messages = BuildMessages(request);

        await foreach (var update in _client.CompleteChatStreamingAsync(messages, options, ct))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (part.Text is not null)
                    yield return part.Text;
            }
        }
    }

    private static ChatCompletionOptions BuildOptions(LlmRequest request)
    {
        var options = new ChatCompletionOptions
        {
            Temperature = request.Temperature,
        };
        if (request.MaxTokens.HasValue)
            options.MaxOutputTokenCount = request.MaxTokens.Value;

        // Set response format for structured output
        if (request.ResponseFormat == LlmResponseFormat.JsonSchema && request.JsonSchema is not null)
        {
            options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: request.JsonSchemaName ?? "response",
                jsonSchema: BinaryData.FromString(request.JsonSchema),
                jsonSchemaIsStrict: true);
        }
        else if (request.ResponseFormat == LlmResponseFormat.Json)
        {
            options.ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat();
        }

        return options;
    }

    private static List<ChatMessage> BuildMessages(LlmRequest request)
    {
        var messages = new List<ChatMessage>();

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            messages.Add(ChatMessage.CreateSystemMessage(request.SystemPrompt));

        foreach (var msg in request.Messages)
        {
            if (msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase) && msg.Images is { Count: > 0 })
            {
                var parts = new List<ChatMessageContentPart>();
                if (!string.IsNullOrEmpty(msg.Content))
                    parts.Add(ChatMessageContentPart.CreateTextPart(msg.Content));
                foreach (var img in msg.Images)
                    parts.Add(ChatMessageContentPart.CreateImagePart(
                        BinaryData.FromBytes(img.Data), img.MimeType));
                messages.Add(ChatMessage.CreateUserMessage(parts));
                continue;
            }

            messages.Add(msg.Role.ToLowerInvariant() switch
            {
                "user" => ChatMessage.CreateUserMessage(msg.Content),
                "assistant" => ChatMessage.CreateAssistantMessage(msg.Content),
                "system" => ChatMessage.CreateSystemMessage(msg.Content),
                _ => ChatMessage.CreateUserMessage(msg.Content)
            });
        }

        return messages;
    }
}
