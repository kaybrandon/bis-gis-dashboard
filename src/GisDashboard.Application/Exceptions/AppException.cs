namespace GisDashboard.Application.Exceptions;

public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message) { }
}

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message) { }
}

public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message) { }
}

public sealed class ValidationException : AppException
{
    public ValidationException(string message) : base(message) { }
}

public sealed class ServiceUnavailableException : AppException
{
    public ServiceUnavailableException(string message) : base(message) { }
}
