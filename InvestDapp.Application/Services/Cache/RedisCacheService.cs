using InvestDapp.Infrastructure.Data.Config;
using InvestDapp.Shared.Models.Trading;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace InvestDapp.Infrastructure.Services.Cache
{
    public interface IRedisCacheService
    {
        Task SetKlineDataAsync(string symbol, string interval, List<KlineData> klines, TimeSpan? expiration = null);
        Task<List<KlineData>> GetKlineDataAsync(string symbol, string interval);
        Task UpdateLatestKlineAsync(string symbol, string interval, KlineData kline);
        
        Task SetMarkPriceAsync(string symbol, MarkPriceData markPrice, TimeSpan? expiration = null);
        Task<MarkPriceData?> GetMarkPriceAsync(string symbol);
        
        Task SetSymbolInfoAsync(List<SymbolInfo> symbols, TimeSpan? expiration = null);
        Task<List<SymbolInfo>> GetSymbolInfoAsync();
        
        Task SetMarketStatsAsync(List<MarketStats> stats, TimeSpan? expiration = null);
        Task<List<MarketStats>> GetMarketStatsAsync();
        
        Task RemoveAsync(string key);
        Task<bool> ExistsAsync(string key);
    }

    public class RedisCacheService : IRedisCacheService
    {
        private readonly IDatabase? _database;
        private readonly IMemoryCache _memoryCache;
        private readonly RedisConfig _config;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly bool _useRedis;

        public RedisCacheService(
            IConnectionMultiplexer redis,
            IMemoryCache memoryCache,
            IOptions<RedisConfig> config,
            ILogger<RedisCacheService> logger)
        {
            _memoryCache = memoryCache;
            _config = config.Value;
            _logger = logger;
            
            if (redis != null && redis.IsConnected)
            {
                _database = redis.GetDatabase(config.Value.Database);
                _useRedis = true;
                _logger.LogInformation("Using Redis for caching");
            }
            else
            {
                _useRedis = false;
                _logger.LogWarning("Redis not available, using in-memory cache as fallback");
            }
        }

        private string GetKey(string key) => $"{_config.KeyPrefix}{key}";

        public async Task SetKlineDataAsync(string symbol, string interval, List<KlineData> klines, TimeSpan? expiration = null)
        {
            try
            {
                var key = GetKey($"klines:{symbol}:{interval}");
                var exp = expiration ?? TimeSpan.FromSeconds(_config.DefaultExpiration);
                
                if (_useRedis && _database != null)
                {
                    var value = JsonConvert.SerializeObject(klines);
                    await _database.StringSetAsync(key, value, exp);
                }
                else
                {
                    _memoryCache.Set(key, klines, exp);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting kline data for {Symbol} {Interval}", symbol, interval);
            }
        }

        public async Task<List<KlineData>> GetKlineDataAsync(string symbol, string interval)
        {
            try
            {
                var key = GetKey($"klines:{symbol}:{interval}");
                
                if (_useRedis && _database != null)
                {
                    var value = await _database.StringGetAsync(key);
                    
                    if (!value.HasValue)
                        return new List<KlineData>();

                    return JsonConvert.DeserializeObject<List<KlineData>>(value!) ?? new List<KlineData>();
                }
                else
                {
                    return _memoryCache.Get<List<KlineData>>(key) ?? new List<KlineData>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting kline data for {Symbol} {Interval}", symbol, interval);
                return new List<KlineData>();
            }
        }

        public async Task UpdateLatestKlineAsync(string symbol, string interval, KlineData kline)
        {
            try
            {
                var klines = await GetKlineDataAsync(symbol, interval);
                
                if (klines.Count == 0)
                {
                    klines.Add(kline);
                }
                else
                {
                    var lastKline = klines.LastOrDefault();
                    if (lastKline != null && lastKline.OpenTime == kline.OpenTime)
                    {
                        // Update existing kline
                        klines[klines.Count - 1] = kline;
                    }
                    else if (kline.IsKlineClosed)
                    {
                        // Add new closed kline
                        klines.Add(kline);
                        
                        // Keep only latest 1000 klines
                        if (klines.Count > 1000)
                        {
                            klines = klines.Skip(klines.Count - 1000).ToList();
                        }
                    }
                }
                
                await SetKlineDataAsync(symbol, interval, klines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating latest kline for {Symbol} {Interval}", symbol, interval);
            }
        }

        public async Task SetMarkPriceAsync(string symbol, MarkPriceData markPrice, TimeSpan? expiration = null)
        {
            try
            {
                var key = GetKey($"markprice:{symbol}");
                var exp = expiration ?? TimeSpan.FromMinutes(1); // Mark price expires faster
                
                if (_useRedis && _database != null)
                {
                    var value = JsonConvert.SerializeObject(markPrice);
                    await _database.StringSetAsync(key, value, exp);
                }
                else
                {
                    _memoryCache.Set(key, markPrice, exp);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting mark price for {Symbol}", symbol);
            }
        }

        public async Task<MarkPriceData?> GetMarkPriceAsync(string symbol)
        {
            try
            {
                var key = GetKey($"markprice:{symbol}");
                
                if (_useRedis && _database != null)
                {
                    var value = await _database.StringGetAsync(key);
                    
                    if (!value.HasValue)
                        return null;

                    return JsonConvert.DeserializeObject<MarkPriceData>(value!);
                }
                else
                {
                    return _memoryCache.Get<MarkPriceData>(key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mark price for {Symbol}", symbol);
                return null;
            }
        }

        public async Task SetSymbolInfoAsync(List<SymbolInfo> symbols, TimeSpan? expiration = null)
        {
            try
            {
                var key = GetKey("symbols:info");
                var exp = expiration ?? TimeSpan.FromHours(6); // Symbol info changes rarely
                
                if (_useRedis && _database != null)
                {
                    var value = JsonConvert.SerializeObject(symbols);
                    await _database.StringSetAsync(key, value, exp);
                }
                else
                {
                    _memoryCache.Set(key, symbols, exp);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting symbol info");
            }
        }

        public async Task<List<SymbolInfo>> GetSymbolInfoAsync()
        {
            try
            {
                var key = GetKey("symbols:info");
                
                if (_useRedis && _database != null)
                {
                    var value = await _database.StringGetAsync(key);
                    
                    if (!value.HasValue)
                        return new List<SymbolInfo>();

                    return JsonConvert.DeserializeObject<List<SymbolInfo>>(value!) ?? new List<SymbolInfo>();
                }
                else
                {
                    return _memoryCache.Get<List<SymbolInfo>>(key) ?? new List<SymbolInfo>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting symbol info");
                return new List<SymbolInfo>();
            }
        }

        public async Task SetMarketStatsAsync(List<MarketStats> stats, TimeSpan? expiration = null)
        {
            try
            {
                var key = GetKey("market:stats");
                var exp = expiration ?? TimeSpan.FromMinutes(5); // Market stats update frequently
                
                if (_useRedis && _database != null)
                {
                    var value = JsonConvert.SerializeObject(stats);
                    await _database.StringSetAsync(key, value, exp);
                }
                else
                {
                    _memoryCache.Set(key, stats, exp);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting market stats");
            }
        }

        public async Task<List<MarketStats>> GetMarketStatsAsync()
        {
            try
            {
                var key = GetKey("market:stats");
                
                if (_useRedis && _database != null)
                {
                    var value = await _database.StringGetAsync(key);
                    
                    if (!value.HasValue)
                        return new List<MarketStats>();

                    return JsonConvert.DeserializeObject<List<MarketStats>>(value!) ?? new List<MarketStats>();
                }
                else
                {
                    return _memoryCache.Get<List<MarketStats>>(key) ?? new List<MarketStats>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting market stats");
                return new List<MarketStats>();
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                var fullKey = GetKey(key);
                
                if (_useRedis && _database != null)
                {
                    await _database.KeyDeleteAsync(fullKey);
                }
                else
                {
                    _memoryCache.Remove(fullKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing key {Key}", key);
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                var fullKey = GetKey(key);
                
                if (_useRedis && _database != null)
                {
                    return await _database.KeyExistsAsync(fullKey);
                }
                else
                {
                    return _memoryCache.TryGetValue(fullKey, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if key exists {Key}", key);
                return false;
            }
        }
    }
}