using BuildingBlocks.Messaging.Contracts;
using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Kafka;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Profile.Application.Services;

namespace Profile.Infrastructure.Consumers;

public record UserRegisteredIntegrationPayload(Guid UserId, string Email, string DisplayName) : IIntegrationEvent
{
    public string EventType => "auth.user.registered.v1";
    public int EventVersion => 1;
}

public class UserRegisteredConsumer : BackgroundService
{
    private const string TopicName = "auth.user.registered.v1";
    private const string ConsumerName = "profile-service-user-registered";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaConsumerConfig _config;
    private readonly ILogger<UserRegisteredConsumer> _logger;

    public UserRegisteredConsumer(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaConsumerConfig> config,
        ILogger<UserRegisteredConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Kafka consumer for {Topic}...", TopicName);

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _config.BootstrapServers,
            GroupId = _config.GroupId,
            AutoOffsetReset = _config.AutoOffsetReset,
            EnableAutoCommit = false
        };

        // Delay briefly to allow infrastructure / DB to be ready
        await Task.Delay(1000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
                consumer.Subscribe(TopicName);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = consumer.Consume(TimeSpan.FromSeconds(1));
                        if (consumeResult == null || consumeResult.IsPartitionEOF)
                        {
                            continue;
                        }

                        using var scope = _scopeFactory.CreateScope();
                        var processor = scope.ServiceProvider.GetRequiredService<IIdempotentEventProcessor>();
                        var profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();

                        bool processed = await processor.ProcessEventAsync<UserRegisteredIntegrationPayload>(
                            ConsumerName,
                            consumeResult.Message.Value,
                            async (envelope, ct) =>
                            {
                                var payload = envelope.Payload;
                                await profileService.CreateInitialProfileAsync(
                                    payload.UserId,
                                    payload.Email,
                                    payload.DisplayName,
                                    ct);
                            },
                            stoppingToken);

                        if (processed)
                        {
                            consumer.Commit(consumeResult);
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming from {Topic}: {Reason}", TopicName, ex.Error.Reason);
                        await Task.Delay(2000, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Kafka consumer connection error for {Topic}. Retrying in 5s...", TopicName);
                await Task.Delay(5000, stoppingToken);
            }
        }

        _logger.LogInformation("Kafka consumer for {Topic} stopped.", TopicName);
    }
}
