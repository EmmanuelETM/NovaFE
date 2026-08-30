namespace NovaFE.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }

    // Helper muy útil para cuando buscas un ID en la BD y no aparece
    public NotFoundException(string name, object key)
        : base($"Entity '{name}' ({key}) was not found.") { }
}
