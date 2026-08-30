using System.Diagnostics;

namespace NovaFE.IntegrationTests.Fixtures;

/// <summary>
/// Detecta si hay un demonio de Docker disponible.
/// <para>
/// Las pruebas de integración levantan PostgreSQL en un contenedor. Sin esta
/// comprobación, un desarrollador sin Docker (o un pipeline sin demonio) vería
/// la suite en rojo por una razón que no tiene nada que ver con su código.
/// Con ella, esas pruebas se marcan como omitidas.
/// </para>
/// </summary>
public static class DockerAvailability
{
    private static readonly Lazy<bool> Disponible = new(Detectar, isThreadSafe: true);

    public static bool IsAvailable => Disponible.Value;

    private static bool Detectar()
    {
        try
        {
            using var proceso = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info --format \"{{.ServerVersion}}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (proceso is null)
                return false;

            if (!proceso.WaitForExit(TimeSpan.FromSeconds(10)))
            {
                proceso.Kill(entireProcessTree: true);
                return false;
            }

            return proceso.ExitCode == 0;
        }
        catch (Exception)
        {
            // Docker no instalado, no en el PATH, o sin permisos.
            return false;
        }
    }
}
