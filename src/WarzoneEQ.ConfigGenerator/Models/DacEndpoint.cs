namespace WarzoneEQ.ConfigGenerator.Models;

public sealed record DacEndpoint
{
    public string? DeviceDirective { get; }

    public DacEndpoint(string endpointName)
    {
        if (string.IsNullOrWhiteSpace(endpointName))
            throw new ArgumentException("Endpoint name must be non-empty.", nameof(endpointName));
        DeviceDirective = $"Device: {endpointName}";
    }

    private DacEndpoint() => DeviceDirective = null;

    public static readonly DacEndpoint WindowsDefault = new();
}
