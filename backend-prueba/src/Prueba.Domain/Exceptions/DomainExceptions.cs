namespace Prueba.Domain.Exceptions;

public class DomainException(string message) : Exception(message);

public class NotFoundException(string message) : Exception(message);

public class ConflictException(string message) : Exception(message);

public class ForbiddenException(string message) : Exception(message);

public class UnauthorizedException(string message) : Exception(message);
