using Invest.Application.EventService;
using InvestDapp.Infrastructure.Data.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Invest.Application.EventListener
{
    public class CampaignEventListener : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CampaignEventListener> _logger;
        private readonly BlockchainConfig _config;

        public CampaignEventListener(IServiceProvider serviceProvider, ILogger<CampaignEventListener> logger, IOptions<BlockchainConfig> config)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _config = config.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Campaign Event Listener is starting for contract: {ContractAddress}", _config.ContractAddress);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Tạo một scope mới cho mỗi lần chạy để lấy các service scoped (như DbContext)
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var eventService = scope.ServiceProvider.GetRequiredService<CampaignEventService>();
                        await eventService.ProcessNewEventsAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    // Tránh để exception làm sập cả service
                    _logger.LogError(ex, "An unhandled exception occurred in CampaignEventListener.");
                }

                // Đợi một khoảng thời gian trước khi chạy lại
                await Task.Delay(TimeSpan.FromSeconds(_config.PollingIntervalSeconds), stoppingToken);
            }

            _logger.LogInformation("Campaign Event Listener is stopping.");
        }
    }

}
