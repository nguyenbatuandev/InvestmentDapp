using InvestDapp.Infrastructure.Services.Cache;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;

namespace InvestDapp.Application.Services.Trading
{
    public class TradingHub : Hub
    {
        private readonly IRedisCacheService _cacheService;
        private readonly ILogger<TradingHub> _logger;

        public TradingHub(
            IRedisCacheService cacheService,
            ILogger<TradingHub> logger)
        {
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task JoinSymbolRoom(string symbol)
        {
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"symbol:{symbol}");
                
                // Send current data to the new client
                var markPrice = await _cacheService.GetMarkPriceAsync(symbol);
                if (markPrice != null)
                {
                    await Clients.Caller.SendAsync("markPrice", markPrice);
                }
                
                _logger.LogDebug("Client {ConnectionId} joined symbol room: {Symbol}", Context.ConnectionId, symbol);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining symbol room {Symbol} for connection {ConnectionId}", symbol, Context.ConnectionId);
            }
        }

        public async Task LeaveSymbolRoom(string symbol)
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"symbol:{symbol}");
                _logger.LogDebug("Client {ConnectionId} left symbol room: {Symbol}", Context.ConnectionId, symbol);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving symbol room {Symbol} for connection {ConnectionId}", symbol, Context.ConnectionId);
            }
        }

        public async Task JoinUserRoom(string userId)
        {
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
                _logger.LogDebug("Client {ConnectionId} joined user room: {UserId}", Context.ConnectionId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining user room {UserId} for connection {ConnectionId}", userId, Context.ConnectionId);
            }
        }

        public async Task LeaveUserRoom(string userId)
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
                _logger.LogDebug("Client {ConnectionId} left user room: {UserId}", Context.ConnectionId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error leaving user room {UserId} for connection {ConnectionId}", userId, Context.ConnectionId);
            }
        }

        public async Task GetKlineHistory(string symbol, string interval)
        {
            try
            {
                var klines = await _cacheService.GetKlineDataAsync(symbol, interval);
                await Clients.Caller.SendAsync("klineInit", new { symbol, interval, data = klines });
                
                _logger.LogDebug("Sent kline history for {Symbol} {Interval} to {ConnectionId}", symbol, interval, Context.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting kline history for {Symbol} {Interval}", symbol, interval);
            }
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogDebug("Client connected: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogDebug("Client disconnected: {ConnectionId}, Exception: {Exception}", Context.ConnectionId, exception?.Message);
            await base.OnDisconnectedAsync(exception);
        }
    }
}