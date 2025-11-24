using FluentAssertions;
using IntegrationHub.Application.DTOs;
using IntegrationHub.Application.Services;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Events;
using IntegrationHub.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace IntegrationHub.Application.Tests.Services;

public class IntegrationRequestServiceTests
{
    private readonly Mock<IIntegrationRequestRepository> _mockRepository;
    private readonly Mock<IMessageBus> _mockMessageBus;
    private readonly Mock<ILogger<IntegrationRequestService>> _mockLogger;
    private readonly IntegrationRequestService _service;

    public IntegrationRequestServiceTests()
    {
        _mockRepository = new Mock<IIntegrationRequestRepository>();
        _mockMessageBus = new Mock<IMessageBus>();
        _mockLogger = new Mock<ILogger<IntegrationRequestService>>();
        _service = new IntegrationRequestService(_mockRepository.Object, _mockMessageBus.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateRequest_AndPublishEvent()
    {
        // Arrange
        var command = new CreateIntegrationRequestCommand
        {
            ExternalId = "EXT-12345",
            SourceSystem = "PartnerA",
            TargetSystem = "Totvs",
            Payload = "{\"test\":\"data\"}"
        };
        var correlationId = Guid.NewGuid().ToString();

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<IntegrationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IntegrationRequest req, CancellationToken ct) => req);

        // Act
        var result = await _service.CreateAsync(command, correlationId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ExternalId.Should().Be(command.ExternalId);
        result.SourceSystem.Should().Be(command.SourceSystem);
        result.TargetSystem.Should().Be(command.TargetSystem);
        result.CorrelationId.Should().Be(correlationId);
        result.Status.Should().Be("Received");

        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<IntegrationRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockMessageBus.Verify(
            m => m.PublishAsync(
                It.IsAny<IntegrationRequestCreated>(),
                "integration-request-created",
                correlationId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnDto_WhenRequestExists()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        var request = new IntegrationRequest(
            "EXT-12345",
            "PartnerA",
            "Totvs",
            "{\"test\":\"data\"}",
            Guid.NewGuid().ToString());

        _mockRepository
            .Setup(r => r.GetByIdAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        // Act
        var result = await _service.GetByIdAsync(requestId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.ExternalId.Should().Be("EXT-12345");
        result.SourceSystem.Should().Be("PartnerA");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenRequestDoesNotExist()
    {
        // Arrange
        var requestId = Guid.NewGuid();
        
        _mockRepository
            .Setup(r => r.GetByIdAsync(requestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IntegrationRequest?)null);

        // Act
        var result = await _service.GetByIdAsync(requestId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllRequests()
    {
        // Arrange
        var requests = new List<IntegrationRequest>
        {
            new("EXT-001", "PartnerA", "Totvs", "{}", Guid.NewGuid().ToString()),
            new("EXT-002", "PartnerB", "Totvs", "{}", Guid.NewGuid().ToString())
        };

        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(requests);

        // Act
        var result = await _service.GetAllAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
    }
}
