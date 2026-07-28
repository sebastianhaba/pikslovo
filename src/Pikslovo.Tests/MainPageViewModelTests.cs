using FluentAssertions;
using NUnit.Framework;
using Pikslovo;

namespace Pikslovo.Tests;

public sealed class MainPageViewModelTests
{
    [Test]
    public void LoadDefaults_restores_expected_initial_state()
    {
        var viewModel = new MainPageViewModel
        {
            ApiKey = "abc",
            SourceLanguage = "en",
            TargetLanguage = "de",
            RecognitionConfidence = 0.9f,
            GroupingPower = 0.9f,
            FontScale = 2f,
            HideIdenticalTranslations = true,
            OcrImageScale = 0.5f,
            UseJpegForOcr = false,
            OcrJpegQuality = 50,
            HotkeyCodes = [1, 2],
            GlobalHotkeyEnabled = true,
            FloatingButtonAlwaysVisible = false,
            FloatingButtonScale = 1.5f,
            FloatingButtonHorizontalPosition = 0.4f,
            FloatingButtonVerticalPosition = 0.8f
        };

        viewModel.LoadDefaults();

        viewModel.ApiKey.Should().BeEmpty();
        viewModel.SourceLanguage.Should().Be("ja");
        viewModel.TargetLanguage.Should().Be("pl");
        viewModel.RecognitionConfidence.Should().Be(0.6f);
        viewModel.GroupingPower.Should().BeGreaterThan(0f);
        viewModel.FontScale.Should().Be(1.3f);
        viewModel.HideIdenticalTranslations.Should().BeTrue();
        viewModel.OcrImageScale.Should().Be(1f);
        viewModel.UseJpegForOcr.Should().BeTrue();
        viewModel.OcrJpegQuality.Should().Be(85);
        viewModel.HotkeyCodes.Should().BeEmpty();
        viewModel.GlobalHotkeyEnabled.Should().BeFalse();
        viewModel.FloatingButtonAlwaysVisible.Should().BeTrue();
        viewModel.FloatingButtonScale.Should().Be(1f);
        viewModel.FloatingButtonHorizontalPosition.Should().Be(0.97f);
        viewModel.FloatingButtonVerticalPosition.Should().Be(0.2f);
    }

    [Test]
    public void CreateTranslationSettings_trims_api_key_and_maps_values()
    {
        var viewModel = new MainPageViewModel
        {
            ApiKey = "  key  ",
            SourceLanguage = "ja",
            TargetLanguage = "pl",
            RecognitionConfidence = 0.7f,
            GroupingPower = 0.8f,
            FontScale = 1.4f,
            HideIdenticalTranslations = true,
            OcrImageScale = 0.5f,
            UseJpegForOcr = false,
            OcrJpegQuality = 72
        };

        var settings = viewModel.CreateTranslationSettings();

        settings.ApiKey.Should().Be("key");
        settings.SourceLanguage.Should().Be("ja");
        settings.TargetLanguage.Should().Be("pl");
        settings.RecognitionConfidence.Should().Be(0.7f);
        settings.GroupingPower.Should().Be(0.8f);
        settings.FontScale.Should().Be(1.4f);
        settings.HideIdenticalTranslations.Should().BeTrue();
        settings.OcrImageScale.Should().Be(0.5f);
        settings.UseJpegForOcr.Should().BeFalse();
        settings.OcrJpegQuality.Should().Be(72);
    }
}
