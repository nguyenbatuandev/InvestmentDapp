using InvestDapp.Infrastructure.Data.Config;
using InvestDapp.Infrastructure.Services.Binance;
using InvestDapp.Shared.Models.Trading;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InvestDapp.Application.Services.Trading
{
    public class MarketDataWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly BinanceConfig _binanceConfig;
        private readonly ILogger<MarketDataWorker> _logger;
        private readonly IHubContext<TradingHub> _hubContext;
        
        private IBinanceRestService? _restService;
        private IBinanceWebSocketService? _webSocketService;
        private Timer? _fallbackTimer;
        private bool _isWebSocketConnected = false;

        public MarketDataWorker(
            IServiceProvider serviceProvider,
            IOptions<BinanceConfig> binanceConfig,
            ILogger<MarketDataWorker> logger,
            IHubContext<TradingHub> hubContext)
        {
            _serviceProvider = serviceProvider;
            _binanceConfig = binanceConfig.Value;
            _logger = logger;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Market Data Worker starting...");
            
            using var scope = _serviceProvider.CreateScope();
            _restService = scope.ServiceProvider.GetRequiredService<IBinanceRestService>();
            _webSocketService = scope.ServiceProvider.GetRequiredService<IBinanceWebSocketService>();

            // Setup WebSocket event handlers
            _webSocketService.OnKlineUpdate += HandleKlineUpdate;
            _webSocketService.OnMarkPriceUpdate += HandleMarkPriceUpdate;
            _webSocketService.OnDisconnected += HandleWebSocketDisconnected;
            _webSocketService.OnReconnected += HandleWebSocketReconnected;

            try
            {
                // Initial data loading
                await LoadInitialDataAsync();
                
                // Start WebSocket
                await StartWebSocketAsync();
                
                // Setup fallback timer
                SetupFallbackTimer();
                
                // Keep service running
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Market Data Worker cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Market Data Worker failed");
            }
        }

        private async Task LoadInitialDataAsync()
        {
            try
            {
                _logger.LogInformation("Loading initial market data...");
                
                // Load exchange info
                var symbolsInfo = await _restService!.GetExchangeInfoAsync();
                
                // Load historical klines for each symbol and interval
                foreach (var symbol in _binanceConfig.SupportedSymbols)
                {
                    foreach (var interval in _binanceConfig.SupportedIntervals)
                    {
                        await _restService.GetKlinesAsync(symbol, interval, _binanceConfig.MaxKlinesHistory);
                        
                        // Small delay to avoid rate limiting
                        await Task.Delay(100);
                    }
                }
                
                // Load mark prices
                await _restService.GetMarkPricesAsync();
                
                // Load market stats
                await _restService.Get24hTickerStatsAsync();
                
                _logger.LogInformation("Initial market data loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading initial market data");
            }
        }

        private async Task StartWebSocketAsync()
        {
            try
            {
                _logger.LogInformation("🚀 Starting Binance WebSocket for {SymbolCount} symbols and {IntervalCount} intervals...", 
                    _binanceConfig.SupportedSymbols.Count, _binanceConfig.SupportedIntervals.Count);
                    
                await _webSocketService!.StartAsync(_binanceConfig.SupportedSymbols, _binanceConfig.SupportedIntervals);
                _isWebSocketConnected = true;
                
                _logger.LogInformation("✅ WebSocket started successfully! Expecting data for ALL intervals: {Intervals}", 
                    string.Join(", ", _binanceConfig.SupportedIntervals));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error starting WebSocket");
                _isWebSocketConnected = false;
            }
        }

        private void SetupFallbackTimer()
        {
            // Check WebSocket status and fallback to REST every 30 seconds
            _fallbackTimer = new Timer(async _ => await FallbackDataUpdate(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        private async Task FallbackDataUpdate()
        {
            if (_isWebSocketConnected) return;
            
            try
            {
                _logger.LogDebug("Executing fallback data update via REST API");
                
                // Update mark prices
                var markPrices = await _restService!.GetMarkPricesAsync();
                foreach (var markPrice in markPrices)
                {
                    await _hubContext.Clients.Group($"symbol:{markPrice.Symbol}")
                        .SendAsync("markPrice", markPrice);
                }
                
                // Update latest klines for 1-minute interval
                foreach (var symbol in _binanceConfig.SupportedSymbols)
                {
                    var klines = await _restService.GetKlinesAsync(symbol, "1m", 1);
                    if (klines.Count > 0)
                        await _hubContext.Clients.Group($"symbol:{symbol}")
                            .SendAsync("klineUpdate", klines[0]);
                    
                    await Task.Delay(50); // Rate limiting
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in fallback data update");
            }
        }

        private async Task HandleKlineUpdate(KlineData kline)
        {
            try
            {
                // Update cache
                // Send to SignalR clients subscribed to this symbol
                await _hubContext.Clients.Group($"symbol:{kline.Symbol}")
                    .SendAsync("klineUpdate", kline);
                    
                _logger.LogDebug("✅ Kline update sent for {Symbol} {Interval}: ${Price} (Volume: {Volume})", 
                    kline.Symbol, kline.Interval, kline.Close, kline.Volume);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error handling kline update for {Symbol} {Interval}", kline.Symbol, kline.Interval);
            }
        }

        private async Task HandleMarkPriceUpdate(MarkPriceData markPrice)
        {
            try
            {
                // Update cache
                // Send to SignalR clients
                await _hubContext.Clients.Group($"symbol:{markPrice.Symbol}")
                    .SendAsync("markPrice", markPrice);
                    
                _logger.LogDebug("Mark price update sent for {Symbol}: {Price}", markPrice.Symbol, markPrice.MarkPrice);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling mark price update for {Symbol}", markPrice.Symbol);
            }
        }

        private async Task HandleWebSocketDisconnected(string reason)
        {
            _isWebSocketConnected = false;
            _logger.LogWarning("WebSocket disconnected: {Reason}", reason);
            
            // Notify clients about disconnection
            await _hubContext.Clients.All.SendAsync("marketDataStatus", new { status = "disconnected", reason });
        }

        private async Task HandleWebSocketReconnected()
        {
            _isWebSocketConnected = true;
            _logger.LogInformation("WebSocket reconnected successfully");
            
            // Notify clients about reconnection
            await _hubContext.Clients.All.SendAsync("marketDataStatus", new { status = "connected" });
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Market Data Worker stopping...");
            
            _fallbackTimer?.Dispose();
            
            if (_webSocketService != null)
            {
                await _webSocketService.StopAsync();
            }
            
            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _fallbackTimer?.Dispose();
            _webSocketService?.Dispose();
            base.Dispose();
        }
    }
}