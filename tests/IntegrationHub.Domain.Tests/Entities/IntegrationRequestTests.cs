using FluentAssertions;
using IntegrationHub.Domain.Entities;
using IntegrationHub.Domain.Enums;

namespace IntegrationHub.Domain.Tests.Entities;

public class IntegrationRequestTests
{
    [Fact]
    public void Constructor_ShouldCreateNewIntegrationRequest_WithReceivedStatus()
    {
        // Arrange
        var externalId = "EXT-12345";
        var sourceSystem = "PartnerA";
        var targetSystem = "Totvs";
        var payload = "{\"data\":\"test\"}";
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var request = new IntegrationRequest(externalId, sourceSystem, targetSystem, payload, correlationId);

        // Assert
        request.Should().NotBeNull();
        request.Id.Should().NotBeEmpty();
        request.ExternalId.Should().Be(externalId);
        request.SourceSystem.Should().Be(sourceSystem);
        request.TargetSystem.Should().Be(targetSystem);
        request.Payload.Should().Be(payload);
        request.CorrelationId.Should().Be(correlationId);
        request.Status.Should().Be(IntegrationStatus.Received);
        request.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        request.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        request.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void MarkAsProcessing_ShouldUpdateStatusToProcessing()
    {
        // Arrange
        var request = CreateSampleRequest();
        var initialUpdatedAt = request.UpdatedAt;

        // Act
        Thread.Sleep(10); // Garante que o UpdatedAt será diferente
        request.MarkAsProcessing();

        // Assert
        request.Status.Should().Be(IntegrationStatus.Processing);
        request.UpdatedAt.Should().BeAfter(initialUpdatedAt);
        request.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void MarkAsFailed_ShouldUpdateStatusToFailedWithErrorMessage()
    {
        // Arrange
        var request = CreateSampleRequest();
        var errorMessage = "Connection timeout";

        // Act
        request.MarkAsFailed(errorMessage);

        // Assert
        request.Status.Should().Be(IntegrationStatus.Failed);
        request.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void MarkAsCompleted_ShouldUpdateStatusToCompleted()
    {
        // Arrange
        var request = CreateSampleRequest();

        // Act
        request.MarkAsCompleted();

        // Assert
        request.Status.Should().Be(IntegrationStatus.Completed);
        request.ErrorMessage.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "sourceSystem", "targetSystem", "{}", "correlationId")]
    [InlineData("externalId", null, "targetSystem", "{}", "correlationId")]
    [InlineData("externalId", "sourceSystem", null, "{}", "correlationId")]
    [InlineData("externalId", "sourceSystem", "targetSystem", null, "correlationId")]
    [InlineData("externalId", "sourceSystem", "targetSystem", "{}", null)]
    public void Constructor_ShouldThrowArgumentNullException_WhenParameterIsNull(
        string? externalId, string? sourceSystem, string? targetSystem, string? payload, string? correlationId)
    {
        // Act
        Action act = () => new IntegrationRequest(externalId!, sourceSystem!, targetSystem!, payload!, correlationId!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static IntegrationRequest CreateSampleRequest()
    {
        return new IntegrationRequest(
            "EXT-12345",
            "PartnerA",
            "Totvs",
            "{\"data\":\"test\"}",
            Guid.NewGuid().ToString());
    }
}
