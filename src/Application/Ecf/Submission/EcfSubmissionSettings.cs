namespace NovaFE.Application.Ecf.Submission;

/// <summary>
/// Parámetros de tiempo del envío a la DGII. La capa Service los llena desde
/// configuración; los defaults sirven para pruebas y arranque.
/// </summary>
public sealed record EcfSubmissionSettings
{
    /// <summary>Presupuesto del fast-path inline del <c>POST /ecf</c> (RF-04: la DGII promedia 200 ms).</summary>
    public TimeSpan SyncWaitBudget { get; init; } = TimeSpan.FromSeconds(8);

    /// <summary>Máximo de consultas de estado que hace el fast-path inline.</summary>
    public int MaxInlinePolls { get; init; } = 3;

    /// <summary>Espera entre consultas del fast-path inline.</summary>
    public TimeSpan InlinePollDelay { get; init; } = TimeSpan.FromMilliseconds(600);

    /// <summary>Primera consulta de estado tras el envío (RF-04.3).</summary>
    public TimeSpan FirstPollDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Reprogramación de las consultas de estado siguientes (RF-04.3: +5 min,
    /// +30 min, +30 min). Al agotarse → revisión manual.
    /// </summary>
    public IReadOnlyList<TimeSpan> PollLadder { get; init; } =
        [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30)];

    /// <summary>
    /// Backoff de los reintentos de <b>envío</b> ante fallos de transporte
    /// (RF-04.7: 2 min → 10 min → 30 min → 2 h). Al agotarse → <c>failed</c>.
    /// </summary>
    public IReadOnlyList<TimeSpan> SubmitBackoff { get; init; } =
        [TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2)];
}
