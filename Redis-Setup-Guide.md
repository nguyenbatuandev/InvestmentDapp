# Redis Setup Guide for InvestDapp

This application uses Redis for caching trading data and market information. Redis is **optional** - the application will automatically fall back to in-memory caching if Redis is not available.

## Quick Setup Options

### Option 1: Docker (Recommended)
```bash
# Pull and run Redis container
docker run -d --name redis-investdapp -p 6379:6379 redis:latest

# Verify it's running
docker ps
```

### Option 2: Windows Installation
1. Download Redis for Windows from: https://github.com/microsoftarchive/redis/releases
2. Extract and run `redis-server.exe`
3. Redis will be available on `localhost:6379`

### Option 3: Run without Redis
The application will work fine without Redis installed. You'll see these log messages:
```
warn: Redis not available, using in-memory cache as fallback
```

## Configuration

Redis settings are in `appsettings.json`:
```json
"Redis": {
    "ConnectionString": "localhost:6379,abortConnect=false",
    "Database": 0,
    "KeyPrefix": "InvestDapp:",
    "DefaultExpiration": 3600
}
```

## Troubleshooting

### Application fails to start with Redis errors
✅ **Fixed**: The application now gracefully handles Redis connection failures and falls back to in-memory cache.

### Want to verify Redis is working
Check the logs for:
- `Using Redis for caching` (Redis is working)
- `Redis not available, using in-memory cache as fallback` (Using fallback)

## Benefits of Redis vs In-Memory Cache

| Feature | Redis | In-Memory |
|---------|-------|-----------|
| Persistence | ✅ Survives app restarts | ❌ Lost on restart |
| Scaling | ✅ Shared across instances | ❌ Per-instance only |
| Memory Usage | ✅ Separate process | ❌ Uses app memory |
| Setup Complexity | ⚠️ Requires installation | ✅ Built-in |