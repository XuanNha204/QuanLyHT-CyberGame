namespace CyberGame.Domain.Exceptions;

public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string message, string code = "domain_error") : base(message)
    {
        Code = code;
    }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string message, string code = "not_found") : base(message, code)
    {
    }
}

public class ConflictException : DomainException
{
    public ConflictException(string message, string code = "conflict") : base(message, code)
    {
    }
}

public class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message, string code = "business_rule_violation") : base(message, code)
    {
    }
}
