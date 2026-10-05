using BuildingBlocks.Messaging.Inbox;
using BuildingBlocks.Messaging.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Profile.Application.Interfaces;
using Profile.Infrastructure.Consumers;
using Profile.Infrastructure.Data;
using Profile.Infrastructure.Inbox;

namespace Profile.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddProfileInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("ProfileDb")
            ?? configuration["PROFILE_DB_CONNECTION"]
            ?? "Server=localhost,1433;Database=ProfileDb;User Id=sa;Password=YourStrong@Passw0rd!2026;TrustServerCertificate=True;";

        services.AddDbContext<ProfileDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(ProfileDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            });
        });

        services.AddScoped<IProfileDbContext>(sp => sp.GetRequiredService<ProfileDbContext>());
        services.AddScoped<IInboxRepository, SqlInboxRepository>();

        // Idempotent event processing
        services.AddScoped<IIdempotentEventProcessor>(sp =>
        {
            var inbox = sp.GetRequiredService<IInboxRepository>();
            var logger = sp.GetRequiredService<ILogger<IdempotentEventProcessor>>();
            var publisher = sp.GetService<IEventPublisher>();
            return new IdempotentEventProcessor(inbox, logger, publisher);
        });

        // Kafka consumer config
        services.Configure<KafkaConsumerConfig>(options =>
        {
            var section = configuration.GetSection("Kafka");
            options.BootstrapServers = section["BootstrapServers"]
                ?? configuration["KAFKA_BOOTSTRAP_SERVERS"]
                ?? "localhost:9092";
            options.GroupId = section["GroupId"] ?? "profile-service-group";
        });

        services.AddHostedService<UserRegisteredConsumer>();

        return services;
    }
}
