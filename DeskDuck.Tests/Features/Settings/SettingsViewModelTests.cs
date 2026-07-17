using DeskDuck.Features.Settings;
using DeskDuck.Features.SystemMonitor;
using DeskDuck.Features.Weather;
using DeskDuck.Models;
using Microsoft.Extensions.Logging;
using Moq;
using System.ComponentModel;
using Xunit;

namespace DeskDuck.Tests.Features.Settings
{
    public class SettingsViewModelTests
    {
        private readonly Mock<ISettingsRepository> _mockRepo;
        private readonly Mock<ILogger<SettingsViewModel>> _mockLogger;

        public SettingsViewModelTests()
        {
            _mockRepo = new Mock<ISettingsRepository>();
            _mockLogger = new Mock<ILogger<SettingsViewModel>>();

            // Setup default response for LoadSettings
            _mockRepo.Setup(r => r.LoadSettings()).Returns(new AppSettingsModel
            {
                General = new GeneralSection { ShowCoordinates = true },
                Publishers = new PublishersSection
                {
                    SystemMonitor = new SystemMonitorOptions { Enabled = true, RamWarningThresholdPercent = 80 },
                    Weather = new WeatherPublisherOptions { Enabled = false, ApiKey = "testkey" }
                }
            });
        }

        [Fact]
        public void Load_InitializesPropertiesFromRepository()
        {
            // Arrange & Act
            var viewModel = new SettingsViewModel(_mockRepo.Object, _mockLogger.Object);

            // Assert
            Assert.True(viewModel.ShowCoordinatesEnabled);
            Assert.True(viewModel.SysMonitorEnabled);
            Assert.Equal(80, viewModel.RamThreshold);
            Assert.False(viewModel.WeatherEnabled);
            Assert.Equal("testkey", viewModel.WeatherApiKey);
        }

        [Fact]
        public void SettingProperty_RaisesPropertyChangedEvent()
        {
            // Arrange
            var viewModel = new SettingsViewModel(_mockRepo.Object, _mockLogger.Object);
            string? changedPropertyName = null;
            viewModel.PropertyChanged += (s, e) => changedPropertyName = e.PropertyName;

            // Act
            viewModel.RamThreshold = 50;

            // Assert
            Assert.Equal(nameof(viewModel.RamThreshold), changedPropertyName);
            Assert.Equal(50, viewModel.RamThreshold);
        }

        [Fact]
        public void Save_WritesUpdatedSettingsToRepository()
        {
            // Arrange
            var viewModel = new SettingsViewModel(_mockRepo.Object, _mockLogger.Object);
            
            // Modify some settings
            viewModel.RamThreshold = 95;
            viewModel.WeatherEnabled = true;
            viewModel.ShowCoordinatesEnabled = false;

            AppSettingsModel? savedSettings = null;
            _mockRepo.Setup(r => r.SaveSettings(It.IsAny<AppSettingsModel>()))
                     .Callback<AppSettingsModel>(s => savedSettings = s);

            // Act
            viewModel.Save();

            // Assert
            _mockRepo.Verify(r => r.SaveSettings(It.IsAny<AppSettingsModel>()), Times.Once);
            Assert.NotNull(savedSettings);
            Assert.NotNull(savedSettings.Publishers?.SystemMonitor);
            
            Assert.Equal(95, savedSettings.Publishers.SystemMonitor.RamWarningThresholdPercent);
            Assert.True(savedSettings.Publishers.Weather?.Enabled);
            Assert.False(savedSettings.General?.ShowCoordinates);
        }
    }
}
