namespace DataIsland.Middleware;

/// <summary>
/// Apply to a controller or action to bypass <see cref="ApiResponseFilter"/>'s
/// { Data, Error } envelope. Use when the endpoint must return a raw JSON body
/// for external-protocol compatibility (e.g. the legacy /api/v1/medicalData
/// endpoints that mirror the dataisland-server contract byte-for-byte).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class SkipApiResponseWrapperAttribute : Attribute
{
}
