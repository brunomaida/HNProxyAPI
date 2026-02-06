using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using HNProxyAPI.Settings;

namespace HNProxyAPI.Tests.Unit
{
    public class InboundAPISettingsTests
    {
        // Helper de Validação (O mesmo usado anteriormente)
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
            var settings = new InboundAPISettings();
            var results = ValidateModel(settings);

            // #ASSERT
            results.Should().BeEmpty("as configurações padrão devem ser válidas para subir a API sem erros");
        }

        [Theory]
        [InlineData(0)]  // Zero requisições não faz sentido
        [InlineData(-1)] // Negativo
        public void MaxRequestsPerWindow_Should_Require_Positive_Value(int invalidValue)
        {
            var settings = new InboundAPISettings { MaxRequestsPerWindow = invalidValue };
            var results = ValidateModel(settings);

            // #ASSERT
            results.Should().Contain(r => r.MemberNames.Contains(nameof(InboundAPISettings.MaxRequestsPerWindow)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        [InlineData(3601)] // Supondo que você limite a janela a no máximo 1 hora (3600s)
        public void RateLimitWindowSeconds_Should_Be_Within_Reasonable_Range(int invalidSeconds)
        {
            var settings = new InboundAPISettings { RateLimitWindowSeconds = invalidSeconds };
            var results = ValidateModel(settings);

            // #ASSERT
            results.Should().Contain(r => r.MemberNames.Contains(nameof(InboundAPISettings.RateLimitWindowSeconds)));
        }

        [Fact]
        public void QueueLimit_Should_Not_Allow_Negative_Numbers()
        {
            var settings = new InboundAPISettings { QueueLimit = -1 };
            var results = ValidateModel(settings);

            // #ASSERT
            results.Should().Contain(r => r.MemberNames.Contains(nameof(InboundAPISettings.QueueLimit)));
        }

        [Fact]
        public void Valid_Custom_Configuration_Should_Pass()
        {
            var settings = new InboundAPISettings
            {
                MaxRequestsPerWindow = 1000,
                RateLimitWindowSeconds = 60,
                QueueLimit = 10
            };
            var results = ValidateModel(settings);

            // #ASSERT
            results.Should().BeEmpty();
        }
    }
}
