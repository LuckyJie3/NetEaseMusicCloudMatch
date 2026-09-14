using System.Net;

namespace NeteaseMusicCloudMatch.Core.Exceptions;

public sealed class NeteaseApiException : Exception
{
    public NeteaseApiException(
        NeteaseErrorKind kind,
        string userMessage,
        string operation,
        string correlationId,
        HttpStatusCode? httpStatus = null,
        int? apiCode = null,
        string? diagnosticMessage = null,
        Exception? innerException = null)
        : base(userMessage, innerException)
    {
        Kind = kind;
        Operation = operation;
        CorrelationId = correlationId;
        HttpStatus = httpStatus;
        ApiCode = apiCode;
        DiagnosticMessage = diagnosticMessage;
    }

    public NeteaseErrorKind Kind { get; }

    public string Operation { get; }

    public string CorrelationId { get; }

    public HttpStatusCode? HttpStatus { get; }

    public int? ApiCode { get; }

    public string? DiagnosticMessage { get; }
}
