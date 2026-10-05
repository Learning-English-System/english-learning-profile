# English Learning - Profile Service

Independent microservice repository managing learner profiles, personal preferences, and Kafka integration events (e.g. `UserRegisteredConsumer`).

## Architecture & Responsibilities
- **Domain**: Learner profile aggregate, learning preferences, daily study targets.
- **Application**: CQRS/Service layer for reading and updating profiles and goals.
- **Infrastructure**: Entity Framework Core with SQL Server, transactional Inbox pattern for idempotent event consumption from Kafka.
- **Api**: ASP.NET Core Minimal/Controller API with JWT authentication, health checks, OpenTelemetry observability.
- **Dependencies**: Consumes `EnglishLearning.BuildingBlocks.*` via NuGet feed.

## Getting Started

### Prerequisites
- .NET 10 SDK
- SQL Server (or Docker MSSQL)
- Kafka message broker

### Build & Run
```bash
# Restore & build
dotnet restore ProfileService.slnx
dotnet build ProfileService.slnx

# Run tests
dotnet test ProfileService.slnx

# Run API
dotnet run --project src/Profile.Api
```

### Docker
```bash
docker build -t english-learning-profile:latest .
docker run -p 5002:5002 english-learning-profile:latest
```
