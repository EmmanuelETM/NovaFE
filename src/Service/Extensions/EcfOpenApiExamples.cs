using System.Text.Json.Nodes;
using NovaFE.Application.Ecf.IssueEcf;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace NovaFE.Service.Extensions;

/// <summary>
/// Le pone un ejemplo realista al cuerpo de <see cref="IssueEcfCommand"/> en el
/// documento OpenAPI. Sin esto, Scalar genera un ejemplo con todos los bloques
/// opcionales en <c>null</c> — ilegible.
/// </summary>
internal sealed class EcfOpenApiExamples : IOpenApiSchemaTransformer
{
    private static JsonObject IssueEcfExample() => new()
    {
        ["type"] = 31,
        ["incomeType"] = "01",
        ["internalNumber"] = "FAC-2026-00042",
        ["buyer"] = new JsonObject { ["rnc"] = "131880681", ["name"] = "Mi Cliente SRL" },
        ["payment"] = new JsonObject
        {
            ["condition"] = "credit",
            ["dueDate"] = "15-03-2026",
            ["methods"] = new JsonArray { new JsonObject { ["type"] = "check_transfer", ["amount"] = 2360.00m } },
        },
        ["lines"] = new JsonArray
        {
            new JsonObject
            {
                ["name"] = "Servicio de consultoría",
                ["kind"] = "service",
                ["quantity"] = 1,
                ["unitOfMeasure"] = "43",
                ["unitPrice"] = 2000.00m,
                ["itbisRate"] = 1,
            },
        },
    };

    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        if (context.JsonTypeInfo.Type == typeof(IssueEcfCommand))
            schema.Example = IssueEcfExample();

        return Task.CompletedTask;
    }
}
