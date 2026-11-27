using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Knock.Hosting.Procesos;
using Microsoft.Extensions.Logging;

namespace Knock.UnitTest.Hosting;

/// <summary>Mediciones del medidor <c>Knock.Procesos</c>. El medidor es del proceso: cada prueba filtra por un identificador propio.</summary>
internal sealed class ProcesosMedidas : IDisposable
{
    private readonly ConcurrentQueue<(string Instrument, Dictionary<string, object?> Tags)> _measurements = new();
    private readonly MeterListener _listener = new();

    public ProcesosMedidas()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == ProcesosMetrics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            _measurements.Enqueue((instrument.Name, tags.ToArray().ToDictionary(x => x.Key, x => x.Value))));
        _listener.Start();
    }

    public List<Dictionary<string, object?>> Of(string instrument, string id) =>
        [.. _measurements.Where(x => x.Instrument == instrument && Equals(x.Tags["id"], id)).Select(x => x.Tags)];

    public void Dispose() => _listener.Dispose();
}

/// <summary>Guarda el texto de cada registro.</summary>
internal sealed class ProcesosRegistro<T> : ILogger<T>
{
    public List<string> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add(formatter(state, exception));
}
