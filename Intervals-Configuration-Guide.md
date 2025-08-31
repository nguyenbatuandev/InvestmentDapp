# 📊 Hướng Dẫn Cấu Hình Khung Thời Gian (Intervals) cho Binance WebSocket

## 🔍 **Vấn Đề Đã Được Giải Quyết**

**Lỗi gốc**: Chỉ có nến 1m hoạt động, các khung thời gian khác không chạy.

**Nguyên nhân**: Trong file `appsettings.json`, cấu hình `SupportedIntervals` chỉ có `["1m"]`.

## ✅ **Giải Pháp Đã Áp Dụng**

### 1. **Cập nhật cấu hình intervals**
```json
"SupportedIntervals": ["1m", "5m", "15m", "30m", "1h", "4h", "1d"]
```

### 2. **Thêm nhiều symbols để test**
```json
"SupportedSymbols": ["BTCUSDT", "ETHUSDT", "BNBUSDT"]
```

### 3. **Cải thiện WebSocket service**
- Ưu tiên intervals phổ biến (1m, 5m, 1h trước)
- Giới hạn số streams để tránh URL quá dài (max 20 streams)
- Xử lý cả single stream và combined streams
- Tự động giảm số streams nếu URL quá dài

## 🎯 **Cấu Hình Khuyến Nghị**

### **Cho Development/Testing:**
```json
{
  "SupportedSymbols": ["BTCUSDT"],
  "SupportedIntervals": ["1m", "5m", "1h"],
  "MaxKlinesHistory": 200
}
```
*→ 3 streams: btcusdt@kline_1m, btcusdt@kline_5m, btcusdt@kline_1h + mark price*

### **Cho Production:**
```json
{
  "SupportedSymbols": ["BTCUSDT", "ETHUSDT", "BNBUSDT"],
  "SupportedIntervals": ["1m", "5m", "15m", "1h", "4h"],
  "MaxKlinesHistory": 500
}
```
*→ 15 streams: 3 symbols × 5 intervals + mark price*

### **Cho Heavy Usage:**
```json
{
  "SupportedSymbols": ["BTCUSDT", "ETHUSDT", "BNBUSDT", "ADAUSDT"],
  "SupportedIntervals": ["1m", "5m", "15m", "30m", "1h", "4h", "1d"],
  "MaxKlinesHistory": 1000
}
```
*→ Tự động giới hạn xuống 20 streams để tránh lỗi URL*

## 📈 **Ưu Tiên Intervals**

WebSocket service sẽ ưu tiên theo thứ tự:
1. **1m** - Realtime cao nhất
2. **5m** - Phân tích ngắn hạn  
3. **15m** - Scalping
4. **1h** - Swing trading
5. **30m** - Phân tích trung hạn
6. **4h** - Trend analysis
7. **1d** - Long-term

## 🚀 **Cách Test Các Intervals**

### 1. **Khởi động ứng dụng**
```bash
dotnet run --project InvestDapp
```

### 2. **Kiểm tra logs**
```
info: Using combined stream WebSocket with 21 streams
info: Connected to Binance WebSocket successfully
debug: Received kline update for BTCUSDT 1m: $43250.50 (Closed: False)
debug: Received kline update for BTCUSDT 5m: $43251.20 (Closed: False)
debug: Received kline update for ETHUSDT 1h: $2650.80 (Closed: True)
```

### 3. **Truy cập Trading Chart**
```
http://localhost:5000/Trading/Chart?symbol=BTCUSDT
```

### 4. **Test switching intervals**
Clicking các buttons: `1m`, `5m`, `15m`, `1h`, `4h`, `1d`

## ⚠️ **Giới Hạn và Lưu Ý**

### **Binance API Limits:**
- **Max 200 streams** per WebSocket connection
- **Max 2000 characters** cho URL
- **Rate limit**: 1200 requests/minute

### **Auto-optimization trong code:**
- Giới hạn max 20 streams để đảm bảo ổn định
- Tự động cắt streams nếu URL quá dài
- Ưu tiên intervals quan trọng nhất

### **Memory usage:**
- Mỗi interval tăng thêm ~100KB data trong cache
- 7 intervals × 3 symbols = ~2MB realtime data

## 🔧 **Troubleshooting**

### **Vấn đề**: Một số intervals không nhận được data
**Giải pháp**:
1. Kiểm tra logs xem stream nào được kết nối
2. Giảm số symbols hoặc intervals nếu quá nhiều
3. Đảm bảo interval format đúng (1m, 5m, 1h, 4h, 1d)

### **Vấn đề**: URL quá dài, lỗi 404
**Giải pháp**:
- Code tự động giảm streams xuống 50%
- Hoặc manual giảm `SupportedSymbols` hoặc `SupportedIntervals`

### **Vấn đề**: Performance chậm
**Giải pháp**:
- Giảm `MaxKlinesHistory` xuống 200-300
- Sử dụng Redis thay vì in-memory cache
- Giới hạn số intervals xuống 3-5 cái chính

## 📊 **Monitoring**

### **Key metrics để theo dõi:**
- **Connection status**: Connected/Disconnected
- **Message rate**: messages/second
- **Memory usage**: Cache size
- **Error rate**: Failed connections

### **Debug commands:**
```bash
# Xem logs realtime
tail -f logs/app.log | grep "kline update"

# Check WebSocket status  
curl http://localhost:5000/api/trading/status
```

Bây giờ bạn có thể test tất cả các khung thời gian từ 1m đến 1d! 🎉