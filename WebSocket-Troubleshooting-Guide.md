# WebSocket Connection Troubleshooting Guide

## Current Issue: Binance WebSocket 404 Errors

The application was getting 404 errors when trying to connect to Binance WebSocket streams. This has been fixed with the following improvements:

## ✅ **Fixes Applied**

### 1. **Simplified Stream Configuration**
- Reduced to single symbol (BTCUSDT) and single interval (1m) for testing
- Uses single stream format: `wss://fstream.binance.com/ws/btcusdt@kline_1m`
- Avoids complex combined stream URLs that can cause 404 errors

### 2. **Smart Stream Selection**
- Single stream for simple cases (1 symbol + 1 interval)
- Combined streams for multiple symbols with proper limits (max 10 streams)
- Automatic fallback to fewer streams if URL becomes too long

### 3. **Improved Message Processing**
- Handles both single stream and combined stream message formats
- Better error handling and logging
- Graceful degradation when messages can't be parsed

## 🔧 **Configuration Settings**

Current test configuration in `appsettings.json`:
```json
"Binance": {
    "WebSocketUrl": "wss://fstream.binance.com/ws",
    "SupportedSymbols": ["BTCUSDT"],
    "SupportedIntervals": ["1m"],
    "WebSocketReconnectDelayMs": 5000
}
```

## 🚀 **Testing the Connection**

1. **Start the application** - should see:
   ```
   info: Using single stream WebSocket: wss://fstream.binance.com/ws/btcusdt@kline_1m
   info: Connected to Binance WebSocket successfully
   ```

2. **Check for data flow**:
   ```
   debug: Received kline update for BTCUSDT 1m: 45123.45 (Closed: False)
   ```

3. **If connection fails**, check logs for:
   - Network connectivity issues
   - Binance API availability
   - URL formatting problems

## 📈 **Expanding to More Symbols**

Once basic connectivity works, you can gradually expand:

### Step 1: Add more intervals
```json
"SupportedIntervals": ["1m", "5m", "1h"]
```

### Step 2: Add more symbols
```json
"SupportedSymbols": ["BTCUSDT", "ETHUSDT", "BNBUSDT"]
```

### Step 3: Monitor performance
- Watch for connection stability
- Check message processing performance
- Monitor for any 404 or rate limit errors

## 🔍 **Common Issues & Solutions**

### Issue: Still getting 404 errors
**Solution**: 
- Verify Binance Futures API is accessible from your network
- Check if your IP is blocked by Binance
- Try with just one symbol first

### Issue: Connection drops frequently
**Solution**:
- Check network stability
- Increase reconnect delay
- Reduce number of streams

### Issue: No data received
**Solution**:
- Check symbol names are correct (must be exact)
- Verify intervals are supported by Binance
- Check logs for JSON parsing errors

## 📊 **URL Format Reference**

### Single Stream (Recommended for testing):
```
wss://fstream.binance.com/ws/btcusdt@kline_1m
```

### Combined Streams (For multiple symbols):
```
wss://fstream.binance.com/ws/stream?streams=btcusdt@kline_1m/ethusdt@kline_1m
```

## ⚡ **Performance Tips**

1. **Start Small**: Begin with 1 symbol and 1 interval
2. **Monitor Limits**: Binance allows max 200 streams per connection
3. **Use Fallback**: The app automatically falls back to REST API if WebSocket fails
4. **Log Analysis**: Check debug logs to understand message flow

## 🎯 **Success Indicators**

You'll know it's working when you see:
- ✅ "Connected to Binance WebSocket successfully"
- ✅ Regular kline updates in logs
- ✅ No 404 or connection errors
- ✅ Real-time price data in your application