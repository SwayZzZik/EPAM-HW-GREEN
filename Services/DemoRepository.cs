using System.Text.Json;
using GreenAndTasty.Backend.Models;

namespace GreenAndTasty.Backend.Services;

// A tiny, synchronized JSON-backed store so POST/DELETE survive server restarts.
// It is intended for a single local process, not multi-instance production hosting.
public sealed class DemoRepository
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private DemoState _state;

    public DemoRepository(IHostEnvironment environment)
    {
        string directory = Environment.GetEnvironmentVariable("APP_DATA_DIR") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(directory))
            directory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "state.json");

        if (File.Exists(_filePath))
        {
            _state = JsonSerializer.Deserialize<DemoState>(File.ReadAllText(_filePath), _options)
                ?? throw new InvalidDataException("State file is empty.");
        }
        else
        {
            _state = SeedData.Create(environment.ContentRootPath);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(_state, _options));
        }
    }

    public async Task<IResult> ReadAsync(Func<DemoState, IResult> action)
    {
        await _mutex.WaitAsync();
        try { return action(_state); }
        finally { _mutex.Release(); }
    }

    public async Task<IResult> UpdateAsync(Func<DemoState, (IResult Response, bool Changed)> action)
    {
        await _mutex.WaitAsync();
        try
        {
            // Only commit the new state when the disk write succeeds.
            var copy = JsonSerializer.Deserialize<DemoState>(JsonSerializer.Serialize(_state, _options), _options)
                ?? throw new InvalidDataException("Could not copy state.");
            var (response, changed) = action(copy);
            if (!changed) return response;

            var temporaryFile = _filePath + ".tmp";
            try
            {
                await File.WriteAllTextAsync(temporaryFile, JsonSerializer.Serialize(copy, _options));
                File.Move(temporaryFile, _filePath, overwrite: true);
                _state = copy;
            }
            finally
            {
                if (File.Exists(temporaryFile)) File.Delete(temporaryFile);
            }
            return response;
        }
        finally { _mutex.Release(); }
    }
}
