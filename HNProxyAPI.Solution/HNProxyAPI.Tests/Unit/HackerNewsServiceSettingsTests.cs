using FluentAssertions;
using HNProxyAPI.Settings;
using System.ComponentModel.DataAnnotations;

namespace HNProxyAPI.Tests.Unit
{
    public class HackerNewsServiceSettingsTests
    {
        // Helper para executar a validação manual (simulando o que o ASP.NET faz no startup)
        private IList<ValidationResult> ValidateModel(object model)
        {
            var validationResults = new List<ValidationResult>();
            var validationContext = new ValidationContext(model, null, null);
            Validator.TryValidateObject(model, validationContext, validationResults, true);
            return validationResults;
        }

        [Fact]
        public void DefaultValues_Should_Be_Valid()
        {
            // Arrange
            var settings = new HackerNewsServiceSettings(); // Usa os defaults

            // Act
            var results = ValidateModel(settings);

            // Assert
            results.Should().BeEmpty("os valores padrão devem sempre ser válidos");
            settings.MaxConcurrentRequests.Should().Be(20);
            settings.RequestTimeoutMs.Should().Be(10000);
            settings.MaxMemoryThresholdBytes.Should().Be(100 * 1024 * 1024);
        }

        [Theory]
        [InlineData("not-a-url")]
        //[InlineData("ftp://invalid-scheme")] // O atributo [Url] aceita http/https por padrão, mas é bom validar
        [InlineData("")] // Required falha aqui também ou Url
        public void UrlBase_Should_Fail_On_Invalid_Format(string invalidUrl)
        {
            // Arrange
            var settings = new HackerNewsServiceSettings
            {
                UrlBase = invalidUrl
            };

            // Act
            var results = ValidateModel(settings);

            // Assert
            results.Should().Contain(r => r.MemberNames.Contains(nameof(HackerNewsServiceSettings.UrlBase)));
            results.Should().HaveCountGreaterThan(0);
        }

        [Theory]
        [InlineData(1999)]  // Abaixo do min (2000)
        [InlineData(15001)] // Acima do max (15000)
        public void RequestTimeoutMs_Should_Respect_Range(int invalidTimeout)
        {
            // Arrange
            var settings = new HackerNewsServiceSettings { RequestTimeoutMs = invalidTimeout };

            // Act
            var results = ValidateModel(settings);

            // Assert
            results.Should().Contain(r => r.MemberNames.Contains(nameof(HackerNewsServiceSettings.RequestTimeoutMs)));
            results.First(r => r.MemberNames.Contains("RequestTimeoutMs"))
                   .ErrorMessage.Should().Contain("Request timeout range");
        }

        [Theory]
        [InlineData(0)]   // Abaixo do min (1)
        [InlineData(201)] // Acima do max (200)
        public void MaxConcurrentRequests_Should_Respect_Range(int invalidConcurrency)
        {
            // Arrange
            var settings = new HackerNewsServiceSettings { MaxConcurrentRequests = invalidConcurrency };

            // Act
            var results = ValidateModel(settings);

            // Assert
            results.Should().Contain(r => r.MemberNames.Contains(nameof(HackerNewsServiceSettings.MaxConcurrentRequests)));
        }

        [Theory]
        [InlineData(63)]   // Abaixo do min (64 bytes)
        [InlineData(1025)] // Acima do max (1024 bytes)
        public void AverageObjectSizeBytes_Should_Respect_Range(int invalidSize)
        {
            var settings = new HackerNewsServiceSettings { AverageObjectSizeBytes = invalidSize };

            var results = ValidateModel(settings);

            results.Should().Contain(r => r.MemberNames.Contains(nameof(HackerNewsServiceSettings.AverageObjectSizeBytes)));
        }

        [Fact]
        public void MaxMemoryThresholdBytes_Should_Fail_If_Too_Low()
        {
            // Arrange
            // Mínimo é 50KB (50 * 1024 = 51200)
            var settings = new HackerNewsServiceSettings { MaxMemoryThresholdBytes = 51199 };

            // Act
            var results = ValidateModel(settings);

            // Assert
            results.Should().Contain(r => r.MemberNames.Contains(nameof(HackerNewsServiceSettings.MaxMemoryThresholdBytes)));
        }

        [Fact]
        public void Settings_Should_Pass_With_Custom_Valid_Values()
        {
            // Arrange
            var settings = new HackerNewsServiceSettings
            {
                HttpClientName = "MyCustomClient",
                UrlBase = "https://valid-url.com",
                UrlBaseStoryById = "https://valid-url.com/{0}",
                RequestTimeoutMs = 5000,
                MaxConcurrentRequests = 45,
                AverageObjectSizeBytes = 512,
                MaxMemoryThresholdBytes = 60 * 1024 // 60KB (Válido > 50KB)
            };

            // Act
            var results = ValidateModel(settings);

            // Assert
            results.Should().BeEmpty();
        }
    }
}