using System.Net;

namespace NeteaseMusicCloudMatch.Core.Models;

public sealed record ApiCallResult<T>(
    T Value,
    HttpStatusCode HttpStatus,
    int? ApiCode,
    TimeSpan Elapsed,
    string CorrelationId);
