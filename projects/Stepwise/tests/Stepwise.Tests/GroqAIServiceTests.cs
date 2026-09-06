using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Stepwise.Core.AI;
using Stepwise.Core.Models;
using Xunit;

namespace Stepwise.Tests;

public class GroqAIServiceTests : IDisposable
{
    private readonly string _tempSettingsDir;

    public GroqAIServiceTests()
    {
        _tempSettingsDir = Path.Combine(Path.GetTempPath(), "Stepwise_AI_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempSettingsDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempSettingsDir))
            {
                Directory.Delete(_tempSettingsDir, true);
            }
        }
        catch { }
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private static Step CreateTestStep()
    {
        return new Step(
            Id: Guid.NewGuid(),
            SequenceIndex: 1,
            Timestamp: DateTime.UtcNow,
            Action: ActionType.LeftClick,
            ClickX: 120,
            ClickY: 45,
            TargetElement: new ElementInfo(
                Name: "Администрирование",
                ControlType: "Button",
                AutomationId: "btnAdmin",
                ClassName: "V8Button",
                ProcessName: "1cv8",
                ProcessId: 100,
                WindowTitle: "1С:Предприятие 8.3",
                WindowHandle: 1,
                BoundingRectangle: new BoundingBox(10, 40, 180, 32),
                FrameworkId: "Win32",
                IsPassword: false
            ),
            Title: "Клик по кнопке",
            Description: "Пользователь нажал кнопку"
        );
    }

    [Fact]
    public async Task EnhanceStepAsync_WhenNoApiKey_ReturnsFailureResultWithoutCrashing()
    {
        var service = new GroqAIService(customSettingsDir: _tempSettingsDir);
        service.StoredApiKey = null;

        var step = CreateTestStep();
        var result = await service.EnhanceStepAsync(step, apiKey: null);

        Assert.False(result.Success);
        Assert.Contains("API-ключ Groq не настроен", result.ErrorMessage);
        Assert.Equal(step.Title, result.Title);
    }

    [Fact]
    public void SettingsPersistence_SavesAndLoadsApiKeyAndModel()
    {
        var service1 = new GroqAIService(customSettingsDir: _tempSettingsDir);
        service1.StoredApiKey = "gsk_test_mock_key_12345";
        service1.SelectedModel = GroqAIService.QwenModel;

        var service2 = new GroqAIService(customSettingsDir: _tempSettingsDir);
        Assert.Equal("gsk_test_mock_key_12345", service2.StoredApiKey);
        Assert.Equal(GroqAIService.QwenModel, service2.SelectedModel);
    }

    [Fact]
    public async Task EnhanceStepAsync_WithSuccessfulResponse_ParsesTitleAndDescription()
    {
        var jsonResponse = """
        {
          "choices": [
            {
              "message": {
                "content": "{\"title\": \"Перейдите в раздел Администрирование\", \"description\": \"В навигационной панели 1С выберите раздел Администрирование для управления правами и резервным копированием.\"}"
              }
            }
          ]
        }
        """;

        var mockHandler = new MockHttpMessageHandler(req =>
        {
            Assert.Equal("Bearer gsk_mock_valid_key", req.Headers.Authorization?.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            };
        });

        var client = new HttpClient(mockHandler);
        var service = new GroqAIService(client, _tempSettingsDir);
        var step = CreateTestStep();

        var result = await service.EnhanceStepAsync(step, apiKey: "gsk_mock_valid_key", model: GroqAIService.DefaultModel);

        Assert.True(result.Success);
        Assert.Equal("Перейдите в раздел Администрирование", result.Title);
        Assert.Contains("В навигационной панели 1С", result.Description);
    }
}
