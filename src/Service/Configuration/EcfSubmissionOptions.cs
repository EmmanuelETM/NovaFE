using System.ComponentModel.DataAnnotations;
using NovaFE.Application.Ecf.Submission;

namespace NovaFE.Service.Configuration;

/// <summary>
/// Configuración del envío de e-CF a la DGII (sección <c>EcfSubmission</c>). Cubre
/// el worker de fondo y el fast-path inline del <c>POST /ecf</c>.
/// </summary>
public sealed class EcfSubmissionOptions
{
    public const string SectionName = "EcfSubmission";

    /// <summary>Arranca el worker de fondo. <c>false</c> en pruebas (disparan el pump a mano).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Segundos entre ticks del worker.</summary>
    [Range(1, 300)]
    public int PollIntervalSeconds { get; set; } = 5;

    /// <summary>Filas por tick.</summary>
    [Range(1, 500)]
    public int BatchSize { get; set; } = 25;

    /// <summary>Minutos tras los que una fila atascada en <c>processing</c> se recupera.</summary>
    [Range(1, 120)]
    public int StuckAfterMinutes { get; set; } = 5;

    /// <summary>Presupuesto del fast-path inline del <c>POST /ecf</c> (segundos).</summary>
    [Range(0, 30)]
    public int SyncWaitBudgetSeconds { get; set; } = 8;

    /// <summary>Consultas de estado que hace el fast-path inline.</summary>
    [Range(0, 10)]
    public int MaxInlinePolls { get; set; } = 3;

    /// <summary>Milisegundos entre consultas del fast-path inline.</summary>
    [Range(50, 5000)]
    public int InlinePollDelayMillis { get; set; } = 600;

    /// <summary>Segundos hasta la primera consulta de estado del worker tras el envío (RF-04.3).</summary>
    [Range(0, 120)]
    public int FirstPollDelaySeconds { get; set; } = 30;

    public TimeSpan PollInterval => TimeSpan.FromSeconds(PollIntervalSeconds);

    public TimeSpan StuckAfter => TimeSpan.FromMinutes(StuckAfterMinutes);

    public TimeSpan SyncWaitBudget => TimeSpan.FromSeconds(SyncWaitBudgetSeconds);

    /// <summary>Proyecta los tiempos que necesitan las capas internas.</summary>
    public EcfSubmissionSettings ToSettings() => new()
    {
        SyncWaitBudget = SyncWaitBudget,
        MaxInlinePolls = MaxInlinePolls,
        InlinePollDelay = TimeSpan.FromMilliseconds(InlinePollDelayMillis),
        FirstPollDelay = TimeSpan.FromSeconds(FirstPollDelaySeconds),
    };
}
