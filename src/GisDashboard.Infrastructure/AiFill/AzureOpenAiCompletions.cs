using Azure.AI.OpenAI;
using GisDashboard.Application.AiFill;
using GisDashboard.Application.Exceptions;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.ClientModel;

namespace GisDashboard.Infrastructure.AiFill;

public sealed class AzureOpenAiCompletions : IAzureOpenAiCompletions
{
    private readonly AzureOpenAIOptions _options;

    public AzureOpenAiCompletions(IOptions<AzureOpenAIOptions> options)
    {
        _options = options.Value;
    }

    public bool IsConfigured => _options.IsConfigured;

    public string Deployment => _options.EffectiveDeployment;

    public Task<string> CompleteJsonAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default) =>
        CompleteJsonAsync(systemPrompt, userPrompt, [], cancellationToken);

    public async Task<string> CompleteJsonAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<AiFillVisionImage> images,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured
            || !Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new ServiceUnavailableException(AzureOpenAIOptions.UnconfiguredMessage);
        }

        var timeoutSeconds = VisionTimeoutSeconds(images);
        var clientOptions = new AzureOpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromSeconds(timeoutSeconds)
        };
        var client = new AzureOpenAIClient(endpoint, new ApiKeyCredential(_options.ApiKey!.Trim()), clientOptions);
        var chat = client.GetChatClient(_options.EffectiveDeployment);
        var completionOptions = new ChatCompletionOptions
        {
            Temperature = 0.1f,
            ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
        };

        var attempts = _options.EffectiveMaxRetries + 1;
        Exception? last = null;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
                ChatCompletion completion = await chat.CompleteChatAsync(
                    [
                        ChatMessage.CreateSystemMessage(systemPrompt),
                        CreateUserMessage(userPrompt, images)
                    ],
                    completionOptions,
                    timeout.Token);

                var text = completion.Content.Count > 0 ? completion.Content[0].Text : null;
                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new ValidationException("AI fill returned an empty response. Try again, or type the fields.");
                }

                return text;
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (ServiceUnavailableException)
            {
                throw;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                last = new ServiceUnavailableException("AI fill timed out. Try again, or type the fields.");
            }
            catch (Exception ex) when (attempt < attempts)
            {
                last = ex;
                await Task.Delay(400 * attempt, cancellationToken);
            }
            catch (Exception)
            {
                throw new ServiceUnavailableException("AI fill failed. Try again, or type the fields.");
            }
        }

        throw last is ServiceUnavailableException unavailable
            ? unavailable
            : new ServiceUnavailableException("AI fill failed. Try again, or type the fields.");
    }

    private int VisionTimeoutSeconds(IReadOnlyList<AiFillVisionImage> images)
    {
        if (images.Count == 0)
        {
            return _options.EffectiveTimeoutSeconds;
        }

        return Math.Clamp(Math.Max(_options.EffectiveTimeoutSeconds, 90), 10, 120);
    }

    private static UserChatMessage CreateUserMessage(string userPrompt, IReadOnlyList<AiFillVisionImage> images)
    {
        if (images.Count == 0)
        {
            return ChatMessage.CreateUserMessage(userPrompt);
        }

        var parts = new List<ChatMessageContentPart>(images.Count + 1)
        {
            ChatMessageContentPart.CreateTextPart(userPrompt)
        };
        foreach (var image in images)
        {
            parts.Add(ChatMessageContentPart.CreateImagePart(
                BinaryData.FromBytes(image.Bytes),
                string.IsNullOrWhiteSpace(image.MediaType) ? "image/png" : image.MediaType,
                ChatImageDetailLevel.High));
        }

        return ChatMessage.CreateUserMessage(parts);
    }
}
