using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;

namespace InvestDapp.Application.Services.Trading
{
    public class TradingHub : Hub
    {
        private readonly ILogger<TradingHub> _logger;
        private readonly IMarketPriceService _marketPriceService;

        public TradingHub(
            ILogger<TradingHub> logger,
            IMarketPriceService marketPriceService)
        {
            _logger = logger;
            _marketPriceService = marketPriceService;
        }

        public async Task JoinSymbolRoom(string symbol)
        {
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"symbol:{symbol}");
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
                // Cache removed: return empty array placeholder
                await Clients.Caller.SendAsync("klineInit", new { symbol, interval, data = Array.Empty<object>() });
                _logger.LogDebug("Sent empty kline history (cache disabled) for {Symbol} {Interval} to {ConnectionId}", symbol, interval, Context.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting kline history for {Symbol} {Interval}", symbol, interval);
            }
        }

        public async Task RequestMarkPrice(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return;
            try
            {
                var px = await _marketPriceService.GetMarkPriceAsync(symbol);
                var payload = new { symbol = symbol, markPrice = px, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
                await Clients.Caller.SendAsync("markPrice", payload);
                _logger.LogDebug("RequestMarkPrice: sent mark price for {Symbol} to {ConnectionId}", symbol, Context.ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestMarkPrice failed for {Symbol}", symbol);
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