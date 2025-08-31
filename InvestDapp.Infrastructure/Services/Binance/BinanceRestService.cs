using InvestDapp.Infrastructure.Data.Config;
using InvestDapp.Shared.Models.Trading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace InvestDapp.Infrastructure.Services.Binance
{
    public interface IBinanceRestService
    {
        Task<List<SymbolInfo>> GetExchangeInfoAsync();
        Task<List<KlineData>> GetKlinesAsync(string symbol, string interval, int limit = 500);
        Task<List<MarkPriceData>> GetMarkPricesAsync();
        Task<MarkPriceData?> GetMarkPriceAsync(string symbol);
        Task<List<FundingRateData>> GetFundingRatesAsync();
        Task<List<MarketStats>> Get24hTickerStatsAsync();
    }

    public class BinanceRestService : IBinanceRestService
    {
        private readonly HttpClient _httpClient;
        private readonly BinanceConfig _config;
        private readonly ILogger<BinanceRestService> _logger;

        public BinanceRestService(
            HttpClient httpClient, 
            IOptions<BinanceConfig> config,
            ILogger<BinanceRestService> logger)
        {
            _httpClient = httpClient;
            _config = config.Value;
            _logger = logger;
            
            _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        }

        public async Task<List<SymbolInfo>> GetExchangeInfoAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/fapi/v1/exchangeInfo");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(content);
                var symbols = data["symbols"]?.ToObject<JArray>();
                
                var result = new List<SymbolInfo>();
                
                if (symbols != null)
                {
                    foreach (var symbol in symbols)
                    {
                        var symbolName = symbol["symbol"]?.ToString();
                        if (string.IsNullOrEmpty(symbolName) || !_config.SupportedSymbols.Contains(symbolName))
                            continue;

                        var filters = symbol["filters"]?.ToObject<JArray>();
                        var priceFilter = filters?.FirstOrDefault(f => f["filterType"]?.ToString() == "PRICE_FILTER");
                        var lotSizeFilter = filters?.FirstOrDefault(f => f["filterType"]?.ToString() == "LOT_SIZE");
                        
                        result.Add(new SymbolInfo
                        {
                            Symbol = symbolName,
                            PriceFilter = decimal.Parse(priceFilter?["tickSize"]?.ToString() ?? "0.01", CultureInfo.InvariantCulture),
                            LotSizeFilter = decimal.Parse(lotSizeFilter?["stepSize"]?.ToString() ?? "0.001", CultureInfo.InvariantCulture),
                            MaxLeverage = 125, // Will be updated from leverageBracket endpoint
                            Status = symbol["status"]?.ToString() ?? "TRADING"
                        });
                    }
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching exchange info");
                return new List<SymbolInfo>();
            }
        }

        public async Task<List<KlineData>> GetKlinesAsync(string symbol, string interval, int limit = 500)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/fapi/v1/klines?symbol={symbol}&interval={interval}&limit={limit}");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var data = JArray.Parse(content);
                
                var result = new List<KlineData>();
                
                foreach (var item in data)
                {
                    var kline = new KlineData
                    {
                        Symbol = symbol,
                        Interval = interval,
                        OpenTime = long.Parse(item[0]?.ToString() ?? "0"),
                        Open = decimal.Parse(item[1]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                        High = decimal.Parse(item[2]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                        Low = decimal.Parse(item[3]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                        Close = decimal.Parse(item[4]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                        Volume = decimal.Parse(item[5]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                        CloseTime = long.Parse(item[6]?.ToString() ?? "0"),
                        IsKlineClosed = true
                    };
                    
                    result.Add(kline);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching klines for {Symbol} {Interval}", symbol, interval);
                return new List<KlineData>();
            }
        }

        public async Task<List<MarkPriceData>> GetMarkPricesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/fapi/v1/premiumIndex");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var data = JArray.Parse(content);
                
                var result = new List<MarkPriceData>();
                
                foreach (var item in data)
                {
                    var symbol = item["symbol"]?.ToString();
                    if (string.IsNullOrEmpty(symbol) || !_config.SupportedSymbols.Contains(symbol))
                        continue;

                    var markPrice = new MarkPriceData
                    {
                        Symbol = symbol,
                        MarkPrice = decimal.Parse(item["markPrice"]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                        IndexPrice = decimal.Parse(item["indexPrice"]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                        FundingRate = decimal.Parse(item["lastFundingRate"]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                        NextFundingTime = long.Parse(item["nextFundingTime"]?.ToString() ?? "0"),
                        EventTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                    
                    result.Add(markPrice);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching mark prices");
                return new List<MarkPriceData>();
            }
        }

        public async Task<MarkPriceData?> GetMarkPriceAsync(string symbol)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/fapi/v1/premiumIndex?symbol={symbol}");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(content);
                
                return new MarkPriceData
                {
                    Symbol = symbol,
                    MarkPrice = decimal.Parse(data["markPrice"]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                    IndexPrice = decimal.Parse(data["indexPrice"]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                    FundingRate = decimal.Parse(data["lastFundingRate"]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                    NextFundingTime = long.Parse(data["nextFundingTime"]?.ToString() ?? "0"),
                    EventTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching mark price for {Symbol}", symbol);
                return null;
            }
        }

        public async Task<List<FundingRateData>> GetFundingRatesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/fapi/v1/fundingRate");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var data = JArray.Parse(content);
                
                var result = new List<FundingRateData>();
                
                foreach (var item in data)
                {
                    var symbol = item["symbol"]?.ToString();
                    if (string.IsNullOrEmpty(symbol) || !_config.SupportedSymbols.Contains(symbol))
                        continue;

                    var fundingRate = new FundingRateData
                    {
                        Symbol = symbol,
                        FundingRate = decimal.Parse(item["fundingRate"]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                        FundingTime = long.Parse(item["fundingTime"]?.ToString() ?? "0")
                    };
                    
                    result.Add(fundingRate);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching funding rates");
                return new List<FundingRateData>();
            }
        }

        public async Task<List<MarketStats>> Get24hTickerStatsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/fapi/v1/ticker/24hr");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var data = JArray.Parse(content);
                
                var result = new List<MarketStats>();
                
                foreach (var item in data)
                {
                    var symbol = item["symbol"]?.ToString();
                    if (string.IsNullOrEmpty(symbol) || !_config.SupportedSymbols.Contains(symbol))
                        continue;

                    var stats = new MarketStats
                    {
                        Symbol = symbol,
                        LastPrice = decimal.Parse(item["lastPrice"]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                        PriceChange = decimal.Parse(item["priceChange"]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                        PriceChangePercent = decimal.Parse(item["priceChangePercent"]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                        Volume = decimal.Parse(item["volume"]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                        QuoteVolume = decimal.Parse(item["quoteVolume"]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                        High24h = decimal.Parse(item["highPrice"]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                        Low24h = decimal.Parse(item["lowPrice"]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                        OpenPrice = decimal.Parse(item["openPrice"]?.ToString() ?? "0", CultureInfo.InvariantCulture),
                        Count = long.Parse(item["count"]?.ToString() ?? "0")
                    };
                    
                    result.Add(stats);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching 24h ticker stats");
                return new List<MarketStats>();
            }
        }
    }
}