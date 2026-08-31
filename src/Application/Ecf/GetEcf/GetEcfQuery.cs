namespace NovaFE.Application.Ecf.GetEcf;

public sealed record GetEcfQuery(Guid Id);

public sealed record GetEcfXmlQuery(Guid Id, bool Rfce = false);
