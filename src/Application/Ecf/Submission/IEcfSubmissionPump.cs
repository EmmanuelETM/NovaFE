namespace NovaFE.Application.Ecf.Submission;

/// <summary>
/// Un tick del worker de envío: recupera filas atascadas, reclama un lote y las
/// procesa (cada una en su propio scope con el tenant fijado). Es un seam para que
/// las pruebas lo disparen de forma determinista en vez de esperar el timer.
/// </summary>
public interface IEcfSubmissionPump
{
    /// <summary>Procesa un lote. Devuelve cuántas filas se procesaron.</summary>
    Task<int> RunOnceAsync(CancellationToken ct = default);
}
