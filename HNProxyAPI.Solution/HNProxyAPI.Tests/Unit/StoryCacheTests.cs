using FluentAssertions;
using HNProxyAPI.Data;
using HNProxyAPI.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Diagnostics.Metrics;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace HNProxyAPI.Tests.Unit
{
    public class StoryCacheTests
    {
        private readonly Mock<IOptionsMonitor<HackerNewsServiceSettings>> _mockSettings;
        private readonly Mock<ILogger<StoryCache>> _mockLogger;
        private readonly Mock<IMeterFactory> _mockMeterFactory;

        // SUT = System Under Test
        private readonly StoryCache sut;

        public StoryCacheTests()
        {
            // 1. Setup do Logger
            _mockLogger = new Mock<ILogger<StoryCache>>();

            // 2. Setup das Configurações (Default: 100MB de limite para testes felizes)
            _mockSettings = new Mock<IOptionsMonitor<HackerNewsServiceSettings>>();
            _mockSettings.Setup(x => x.CurrentValue).Returns(new HackerNewsServiceSettings
            {
                MaxMemoryThresholdBytes = 100 * 1024 * 1024, // 100 MB
                AverageObjectSizeBytes = 1024 // 1 KB default
            });

            // 3. Setup do MeterFactory (Crítico para não dar NullReference no construtor)
            _mockMeterFactory = new Mock<IMeterFactory>();
            // Quando Create for chamado, retorna um Meter real descartável ou Mock
            _mockMeterFactory
                .Setup(f => f.Create(It.IsAny<MeterOptions>()))
                .Returns((MeterOptions options) => new Meter(options.Name, options.Version, options.Tags));

            // Sets the class for every test
            //_sut = new StoryCache(_mockLogger.Object, _mockSettings.Object, _mockMeterFactory.Object);
        }

        #region TryAdd & Memory Limits

        [Fact]
        public void TryAdd_Should_AddItem_When_MemoryIsAvailable()
        {
            // Sets the class for every test
            var sut = new StoryCache(_mockLogger.Object, _mockSettings.Object, _mockMeterFactory.Object);

            // Arrange
            var story = CreateStory(1, 100);

            // Act
            bool result = sut.TryAdd(story);

            // Assert
            result.Should().BeTrue();
            sut.Contains(1).Should().BeTrue();
            // Verifica se o contador interno subiu (indiretamente pelo Count do dictionary)
            // Nota: Não conseguimos testar _currentMemoryUsageBytes diretamente pois é privado, 
            // mas testamos o comportamento de aceitação.
        }

        [Fact]
        public void TryAdd_Should_RejectItem_When_MemoryLimitIsExceeded()
        {
            // Sets the class for every test
            var sut = new StoryCache(_mockLogger.Object, _mockSettings.Object, _mockMeterFactory.Object);

            // Arrange
            // Configura um limite EXTREMAMENTE baixo (1 Byte) para forçar o erro
            _mockSettings.Setup(x => x.CurrentValue).Returns(new HackerNewsServiceSettings
            {
                MaxMemoryThresholdBytes = 1,
                AverageObjectSizeBytes = 50 // O objeto vai ser estimado maior que 1 byte
            });

            var story = CreateStory(2, 100);

            // Act
            bool result = sut.TryAdd(story);

            // Assert
            result.Should().BeFalse("o cache deveria rejeitar a inserção por falta de memória");
            sut.Contains(2).Should().BeFalse();

            // Verifica se logou o warning correto
            VerifyLogger(LogLevel.Warning, "Memory Full");
        }

        [Fact]
        public void TryAdd_Should_ReturnTrue_If_ItemAlreadyExists()
        {
            // Sets the class for every test
            var sut = new StoryCache(_mockLogger.Object, _mockSettings.Object, _mockMeterFactory.Object);

            // Arrange
            var story = CreateStory(1, 100);
            sut.TryAdd(story);

            // Act - Adiciona o mesmo ID de novo
            bool result = sut.TryAdd(story);

            // Assert
            result.Should().BeTrue("TryAdd deve ser idempotente");
            sut.Contains(1).Should().BeTrue();
        }

        #endregion

        #region RemoveOldIds

        [Fact]
        public void RemoveOldIds_Should_RemoveItems_FromCache()
        {
            // Sets the class for every test
            var sut = new StoryCache(_mockLogger.Object, _mockSettings.Object, _mockMeterFactory.Object);

            // Arrange
            sut.TryAdd(CreateStory(10, 100));
            sut.TryAdd(CreateStory(20, 200));
            sut.TryAdd(CreateStory(30, 300));

            var idsToRemove = new[] { 10, 20 };

            // Act
            sut.RemoveOldIds(idsToRemove);

            // Assert
            sut.Contains(10).Should().BeFalse();
            sut.Contains(20).Should().BeFalse();
            sut.Contains(30).Should().BeTrue("ID 30 não estava na lista de remoção");
        }

        [Fact]
        public void RemoveOldIds_Should_Handle_EmptyList_Gracefully()
        {
            // Sets the class for every test
            var sut = new StoryCache(_mockLogger.Object, _mockSettings.Object, _mockMeterFactory.Object);

            // Arrange
            sut.TryAdd(CreateStory(1, 100));

            // Act
            sut.RemoveOldIds(new int[] { });

            // Assert
            sut.Contains(1).Should().BeTrue();
        }

        #endregion

        #region RebuildOrderedListAsync (Sorting & Snapshot)

        [Fact]
        public async Task RebuildOrderedListAsync_Should_SortByScore_Descending()
        {
            // Sets the class for every test
            var sut = new StoryCache(_mockLogger.Object, _mockSettings.Object, _mockMeterFactory.Object);

            // Arrange
            sut.TryAdd(CreateStory(1, score: 10));  // Baixo
            sut.TryAdd(CreateStory(2, score: 100)); // Alto
            sut.TryAdd(CreateStory(3, score: 50));  // Médio

            // Act
            await sut.RebuildOrderedListAsync();
            var list = sut.GetOrderedList();

            // Assert
            list.Should().HaveCount(3);
            list[0].id.Should().Be(2); // Score 100
            list[1].id.Should().Be(3); // Score 50
            list[2].id.Should().Be(1); // Score 10
        }

        [Fact]
        public async Task RebuildOrderedListAsync_Should_Update_Snapshot_Instance()
        {
            // Sets the class for every test
            var sut = new StoryCache(_mockLogger.Object, _mockSettings.Object, _mockMeterFactory.Object);

            // Arrange
            sut.TryAdd(CreateStory(1, 10));
            await sut.RebuildOrderedListAsync();
            var snapshot1 = sut.GetOrderedList();

            // Act
            sut.TryAdd(CreateStory(2, 20));
            await sut.RebuildOrderedListAsync();
            var snapshot2 = sut.GetOrderedList();

            // Assert
            snapshot1.Should().NotBeSameAs(snapshot2, "o snapshot deve ser uma nova referência imutável após o rebuild");
            snapshot1.Should().HaveCount(1, "o snapshot antigo não deve ser alterado");
            snapshot2.Should().HaveCount(2, "o snapshot novo deve conter os novos dados");
        }

        #endregion

        #region Helpers

        private Story CreateStory(int id, int score)
        {
            // Sets the class for every test
            var sut = new StoryCache(_mockLogger.Object, _mockSettings.Object, _mockMeterFactory.Object);

            // Criação manual da struct Story baseada no que você usou anteriormente
            return new Story
            {
                id = id,
                title = $"Title {id}",
                score = score,
                postedBy = "author",
                time = DateTime.UtcNow,
                uri = "http://test.com"
            };
        }

        private void VerifyLogger(LogLevel level, string messagePart)
        {
            // Sets the class for every test
            var sut = new StoryCache(_mockLogger.Object, _mockSettings.Object, _mockMeterFactory.Object);

            _mockLogger.Verify(
                x => x.Log(
                    level,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(messagePart)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        #endregion
    }
}