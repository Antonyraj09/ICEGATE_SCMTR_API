using Microsoft.Extensions.Options;

namespace Icegate.Integration.Tests;

/// <summary>Minimal IOptionsMonitor&lt;T&gt; stub for unit tests - no reload support needed.</summary>
public class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    public TestOptionsMonitor(T currentValue)
    {
        CurrentValue = currentValue;
    }

    public T CurrentValue { get; }

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

public static class TestOptionsMonitor
{
    public static IOptionsMonitor<T> Create<T>(T value) => new TestOptionsMonitor<T>(value);
}
