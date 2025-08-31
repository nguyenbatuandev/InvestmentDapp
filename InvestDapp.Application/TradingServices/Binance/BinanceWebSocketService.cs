using InvestDapp.Infrastructure.Data.Config;
using InvestDapp.Shared.Models.Trading;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.WebSockets;
using System.Text;

namespace InvestDapp.Infrastructure.Services.Binance
{
    public interface IBinanceWebSocketService : IDisposable
    {
        event Func<KlineData, Task>? OnKlineUpdate;
        event Func<MarkPriceData, Task>? OnMarkPriceUpdate;
        event Func<string, Task>? OnDisconnected;
        event Func<Task>? OnReconnected;
        
        Task StartAsync(List<string> symbols, List<string> intervals);
        Task StopAsync();
        bool IsConnected { get; }
    }

    public class BinanceWebSocketService : IBinanceWebSocketService, IDisposable
    {
        private readonly BinanceConfig _config;
        private readonly ILogger<BinanceWebSocketService> _logger;
        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _cancellationTokenSource;
        private readonly object _lockObject = new();
        private bool _isStarted = false;
        private bool _disposed = false;

        public event Func<KlineData, Task>? OnKlineUpdate;
        public event Func<MarkPriceData, Task>? OnMarkPriceUpdate;
        public event Func<string, Task>? OnDisconnected;
        public event Func<Task>? OnReconnected;

        public bool IsConnected => _webSocket?.State == WebSocketState.Open;

        public BinanceWebSocketService(
            IOptions<BinanceConfig> config,
            ILogger<BinanceWebSocketService> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        public async Task StartAsync(List<string> symbols, List<string> intervals)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BinanceWebSocketService));

            lock (_lockObject)
            {
                if (_isStarted) return;
                _isStarted = true;
            }

            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                await ConnectAndListenAsync(symbols, intervals, _cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting Binance WebSocket service");
                lock (_lockObject)
                {
                    _isStarted = false;
                }
                throw;
            }
        }

        public async Task StopAsync()
        {
            if (_disposed) return;

            lock (_lockObject)
            {
                if (!_isStarted) return;
                _isStarted = false;
            }

            try
            {
                _cancellationTokenSource?.Cancel();
                
                if (_webSocket?.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Service stopping", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Binance WebSocket service");
            }
            finally
            {
                CleanupResources();
            }
        }

        private async Task ConnectAndListenAsync(List<string> symbols, List<string> intervals, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && !_disposed)
            {
                try
                {
                    _webSocket = new ClientWebSocket();
                    
                    // **Add headers to improve compatibility**
                    _webSocket.Options.SetRequestHeader("User-Agent", "InvestDapp/1.0");
                    _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
                    
                    string wsUrl;
                    
                    // Tạo danh sách streams thông minh
                    var streamNames = BuildStreamNames(symbols, intervals);
                    
                    // **SMART FALLBACK STRATEGY**
                    if (streamNames.Count == 1)
                    {
                        // Single stream - đơn giản và ổn định nhất
                        wsUrl = $"{_config.WebSocketUrl}/{streamNames[0]}";
                        _logger.LogInformation("🚀 Using single stream: {StreamName}", streamNames[0]);
                    }
                    else // Combined streams cho tất cả multi-interval cases
                    {
                        // Combined streams - sử dụng endpoint đúng của Binance Futures
                        var streamParam = string.Join("/", streamNames);
                        wsUrl = $"wss://fstream.binance.com/ws/{streamParam}";
                        _logger.LogInformation("� Using combined streams: {Count} streams", streamNames.Count);
                        _logger.LogInformation("� Total streams available: {Count} - Will implement rotation later", streamNames.Count);
                    }
                    
                    _logger.LogInformation("🔗 FULL WebSocket URL: {Url}", wsUrl);
                    _logger.LogDebug("Connecting to: {Url}", 
                        wsUrl.Length > 150 ? wsUrl.Substring(0, 150) + "..." : wsUrl);
                    
                    await _webSocket.ConnectAsync(new Uri(wsUrl), cancellationToken);
                    
                    _logger.LogInformation("✅ Connected to Binance WebSocket successfully!");
                    _logger.LogInformation("📊 Receiving data for {Count} streams", streamNames.Count);
                    
                    if (OnReconnected != null)
                        await OnReconnected.Invoke();

                    await ListenForMessagesAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Binance WebSocket connection cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ WebSocket connection failed. Retrying in {Delay}ms", _config.WebSocketReconnectDelayMs);
                    
                    if (OnDisconnected != null)
                        await OnDisconnected.Invoke($"Connection failed: {ex.Message}");

                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(_config.WebSocketReconnectDelayMs, cancellationToken);
                    }
                }
                finally
                {
                    _webSocket?.Dispose();
                    _webSocket = null;
                }
            }
        }

        private List<string> BuildStreamNames(List<string> symbols, List<string> intervals)
        {
            var streams = new List<string>();
            
            // **MULTI-INTERVAL MODE: Hỗ trợ nhiều intervals cho mỗi symbol**
            foreach (var symbol in symbols)
            {
                var lowerSymbol = symbol.ToLower();
                
                // Thêm kline streams cho tất cả intervals
                foreach (var interval in intervals)
                {
                    streams.Add($"{lowerSymbol}@kline_{interval}");
                }
                
                // Thêm mark price stream cho symbol (chỉ 1 lần mỗi symbol)
                streams.Add($"{lowerSymbol}@markPrice");
            }
            
            _logger.LogInformation("📊 Built {Count} streams for {SymbolCount} symbols and {IntervalCount} intervals", 
                streams.Count, symbols.Count, intervals.Count);
            
            _logger.LogDebug("🔗 Streams: {Streams}", string.Join(", ", streams.Take(10)));
            
            if (streams.Count > 20)
            {
                _logger.LogWarning("⚠️ Too many streams ({Count}), may cause performance issues", streams.Count);
            }
            
            return streams;
        }
        
        private static int GetIntervalPriority(string interval)
        {
            return interval switch
            {
                "1m" => 1,
                "5m" => 2,
                "15m" => 3,
                "1h" => 4,
                "30m" => 5,
                "4h" => 6,
                "1d" => 7,
                _ => 10
            };
        }

        private async Task ListenForMessagesAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            
            while (_webSocket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested && !_disposed)
            {
                try
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await ProcessMessageAsync(message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogWarning("WebSocket connection closed by server. Close status: {Status}, Description: {Description}", 
                            result.CloseStatus, result.CloseStatusDescription);
                        break;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("WebSocket receive operation cancelled");
                    break;
                }
                catch (WebSocketException wsEx)
                {
                    _logger.LogError(wsEx, "WebSocket error occurred during message reception");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error receiving WebSocket message");
                    break;
                }
            }
        }

        private async Task ProcessMessageAsync(string message)
        {
            if (_disposed) return;

            try
            {
                var data = JObject.Parse(message);
                
                // Kiểm tra định dạng combined stream
                if (data["stream"] != null)
                {
                    // Combined stream format: {"stream":"btcusdt@kline_1m","data":{...}}
                    var stream = data["stream"]?.ToString();
                    var eventData = data["data"];
                    
                    if (string.IsNullOrEmpty(stream) || eventData == null)
                    {
                        _logger.LogDebug("Received message with empty stream or data");
                        return;
                    }

                    if (stream.Contains("@kline_"))
                    {
                        await ProcessKlineMessage(eventData);
                    }
                    else if (stream.Contains("@markPrice"))
                    {
                        await ProcessMarkPriceMessage(eventData);
                    }
                    else
                    {
                        _logger.LogDebug("Received unknown stream type: {Stream}", stream);
                    }
                }
                else if (data["e"] != null)
                {
                    // Single stream format: {"e":"kline","E":123456789,"s":"BTCUSDT",...}
                    var eventType = data["e"]?.ToString();
                    
                    if (eventType == "kline")
                    {
                        await ProcessKlineMessage(data);
                    }
                    else if (eventType == "markPriceUpdate")
                    {
                        await ProcessMarkPriceMessage(data);
                    }
                    else
                    {
                        _logger.LogDebug("Received unknown event type: {EventType}", eventType);
                    }
                }
                else
                {
                    _logger.LogDebug("Received unknown message format");
                }
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Error parsing WebSocket message JSON");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing WebSocket message");
            }
        }

        private async Task ProcessKlineMessage(JToken eventData)
        {
            if (_disposed) return;

            try
            {
                // Xử lý cả định dạng single stream và combined stream
                JToken? klineData = null;
                
                if (eventData["k"] != null)
                {
                    // Combined stream format hoặc single stream với field 'k'
                    klineData = eventData["k"];
                }
                else if (eventData["s"] != null && eventData["i"] != null)
                {
                    // Direct kline data format
                    klineData = eventData;
                }
                
                if (klineData == null) 
                {
                    _logger.LogWarning("Received kline message without recognizable kline data");
                    return;
                }

                var symbol = klineData["s"]?.ToString();
                var interval = klineData["i"]?.ToString();

                if (string.IsNullOrEmpty(symbol) || string.IsNullOrEmpty(interval))
                {
                    _logger.LogWarning("Received kline message with missing symbol or interval");
                    return;
                }

                var kline = new KlineData
                {
                    Symbol = symbol,
                    Interval = interval,
                    OpenTime = ParseLong(klineData["t"]?.ToString()),
                    CloseTime = ParseLong(klineData["T"]?.ToString()),
                    Open = ParseDecimal(klineData["o"]?.ToString()),
                    High = ParseDecimal(klineData["h"]?.ToString()),
                    Low = ParseDecimal(klineData["l"]?.ToString()),
                    Close = ParseDecimal(klineData["c"]?.ToString()),
                    Volume = ParseDecimal(klineData["v"]?.ToString()),
                    IsKlineClosed = ParseBool(klineData["x"]?.ToString())
                };

                _logger.LogDebug("Received kline update for {Symbol} {Interval}: ${Price} (Closed: {IsClosed})", 
                    symbol, interval, kline.Close, kline.IsKlineClosed);

                if (OnKlineUpdate != null)
                {
                    await OnKlineUpdate.Invoke(kline);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing kline message");
            }
        }

        private async Task ProcessMarkPriceMessage(JToken eventData)
        {
            if (_disposed) return;

            try
            {
                var symbol = eventData["s"]?.ToString();
                if (string.IsNullOrEmpty(symbol))
                {
                    _logger.LogWarning("Received mark price message with missing symbol");
                    return;
                }

                var markPrice = new MarkPriceData
                {
                    Symbol = symbol,
                    MarkPrice = ParseDecimal(eventData["p"]?.ToString()),
                    IndexPrice = ParseDecimal(eventData["i"]?.ToString()),
                    FundingRate = ParseDecimal(eventData["r"]?.ToString()),
                    NextFundingTime = ParseLong(eventData["T"]?.ToString()),
                    EventTime = ParseLong(eventData["E"]?.ToString())
                };

                _logger.LogDebug("Received mark price update for {Symbol}: ${Price}", symbol, markPrice.MarkPrice);

                if (OnMarkPriceUpdate != null)
                {
                    await OnMarkPriceUpdate.Invoke(markPrice);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing mark price message");
            }
        }

        private static long ParseLong(string? value)
        {
            return long.TryParse(value, out var result) ? result : 0;
        }

        private static decimal ParseDecimal(string? value)
        {
            return decimal.TryParse(value, out var result) ? result : 0;
        }

        private static bool ParseBool(string? value)
        {
            return bool.TryParse(value, out var result) && result;
        }

        private void CleanupResources()
        {
            try
            {
                _webSocket?.Dispose();
                _webSocket = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing WebSocket");
            }

            try
            {
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing CancellationTokenSource");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            try
            {
                StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal");
            }

            CleanupResources();
            GC.SuppressFinalize(this);
        }

        ~BinanceWebSocketService()
        {
            Dispose();
        }
    }
}