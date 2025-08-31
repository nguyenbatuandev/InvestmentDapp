# 🔧 Quick Fix for WebSocket 404 Error

## 🚨 **Current Issue**
```
fail: Binance WebSocket connection failed
WebSocketException: The server returned status code '404' when status code '101' was expected
```

## ✅ **Solution Applied**

### 1. **Reduced Configuration (Safe Mode)**
```json
{
  "SupportedSymbols": ["BTCUSDT", "ETHUSDT"],
  "SupportedIntervals": ["1m", "5m", "15m", "1h", "4h"],
  "MaxStreams": 8 + 1 mark price = 9 total streams
}
```

### 2. **Smart Stream Prioritization**
- **Priority 1**: BTCUSDT (most liquid)
- **Priority 2**: Core intervals (1m, 5m, 1h, 4h)
- **Maximum**: 8 kline streams + 1 mark price = 9 streams total

### 3. **URL Length Protection**
- **Hard limit**: 10 streams maximum
- **URL length**: < 1500 characters
- **Auto-reduction**: If URL too long, cut to 5 streams

## 🎯 **Expected Result**

With new configuration, you should see:
```
info: Built 9 prioritized streams for 2 symbols and 4 intervals
info: Using combined stream WebSocket with 9 streams
info: ✅ Connected to Binance WebSocket successfully
debug: Received kline update for BTCUSDT 1m: $43250.50
debug: Received kline update for ETHUSDT 5m: $2651.20
```

## 🧪 **Test Progressive Scaling**

### **Step 1: Start with minimum (GUARANTEED to work)**
```json
{
  "SupportedSymbols": ["BTCUSDT"],
  "SupportedIntervals": ["1m"]
}
```
*→ 1 stream: `btcusdt@kline_1m`*

### **Step 2: Add intervals**
```json
{
  "SupportedSymbols": ["BTCUSDT"],
  "SupportedIntervals": ["1m", "5m", "1h"]
}
```
*→ 4 streams: 3 kline + 1 mark price*

### **Step 3: Add symbols**
```json
{
  "SupportedSymbols": ["BTCUSDT", "ETHUSDT"],
  "SupportedIntervals": ["1m", "5m", "1h"]
}
```
*→ 7 streams: 6 kline + 1 mark price*

### **Step 4: Current configuration (should work)**
```json
{
  "SupportedSymbols": ["BTCUSDT", "ETHUSDT"],
  "SupportedIntervals": ["1m", "5m", "15m", "1h", "4h"]
}
```
*→ 9 streams: 8 kline + 1 mark price*

## 🚀 **Troubleshooting Steps**

1. **Start the app** and check logs for:
   ```
   info: Built X prioritized streams
   info: ✅ Connected to Binance WebSocket successfully
   ```

2. **If still 404**: Temporarily use Step 1 config (single symbol + interval)

3. **If works**: Gradually increase symbols/intervals

4. **Check network**: Make sure you can reach `wss://fstream.binance.com`

## 📊 **Stream Priority Logic**

Current prioritization (in order):
1. **1m** - Real-time price action
2. **5m** - Short-term scalping  
3. **15m** - Quick swing trades
4. **1h** - Medium-term analysis
5. **4h** - Trend confirmation

Symbols: BTCUSDT first (highest volume), then ETHUSDT

## 🎛️ **Manual Override**

If you need different intervals, edit `GetIntervalPriority()` in WebSocket service:
```csharp
private static int GetIntervalPriority(string interval)
{
    return interval switch
    {
        "1h" => 1,  // Make 1h highest priority
        "4h" => 2,  // Then 4h
        "1m" => 3,  // Then 1m
        // ... etc
    };
}
```

This fix should resolve the 404 errors! 🎉