using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

var redisConnectionString = builder.Configuration.GetConnectionString("RedisCache");

builder.Services.AddDistributedRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "SampleInstance";
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseResponseCaching();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weather1", (IDistributedCache distributedCache, IMemoryCache memoryCache) =>
{
    
    if (memoryCache.TryGetValue("GetWeather", out var cachedForecast))
    {
        return cachedForecast;
    }

    
    var distributedCacheKey = "GetWeather";
    var cachedData = distributedCache.GetString(distributedCacheKey);
    
    if (cachedData is { Length: > 0 })
    {
        var forecast = System.Text.Json.JsonSerializer.Deserialize<IEnumerable<WeatherForecast>>(cachedData);
        
        var weatherForecasts = forecast as WeatherForecast[] ?? forecast?.ToArray();
        memoryCache.Set("GetWeather", weatherForecasts, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1) 
        });
        return weatherForecasts;
    }

    // Generate new data if not found in caches
    var forecastData = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast(
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        )).ToArray();

    // Save data to caches
    var serializedData = System.Text.Json.JsonSerializer.Serialize(forecastData);
    distributedCache.SetString(distributedCacheKey, serializedData, new DistributedCacheEntryOptions
    {
        SlidingExpiration = TimeSpan.FromMinutes(10),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1) 
    });

    memoryCache.Set("GetWeather", forecastData, new MemoryCacheEntryOptions
    {
        SlidingExpiration = TimeSpan.FromMinutes(5),
        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1) 
    });

    return forecastData;
})
.WithName("GetWeather")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
