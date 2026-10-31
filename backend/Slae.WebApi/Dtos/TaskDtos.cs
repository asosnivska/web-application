using System.Text.Json;
using Slae.Application.Interfaces;
using Slae.Application.Models;

namespace Slae.WebApi.Dtos;

public class CreateTaskRequest
{
    public int Dimension { get; set; }
    public double Epsilon { get; set; } = 1e-6;
    public int MaxIterations { get; set; } = 10000;
    public int Seed { get; set; } = 1;
    public double Dominance { get; set; } = 1.01;
    public double[][]? Matrix { get; set; }
    public double[]? Vector { get; set; }

    public JacobiParams ToParams() => new()
    {
        Dimension = Dimension,
        Epsilon = Epsilon,
        MaxIterations = MaxIterations,
        Seed = Seed,
        Dominance = Dominance,
        Matrix = Matrix,
        Vector = Vector
    };
}

public record TaskSummaryResponse(
    Guid Id, string Status, int Progress, int Dimension, DateTime CreatedAt, DateTime? CompletedAt);

public record TaskDetailsResponse(
    Guid Id, string Status, int Progress, int Dimension, DateTime CreatedAt, DateTime? CompletedAt,
    JsonElement? Result, string? Error);

public static class TaskMapping
{
    public static TaskSummaryResponse ToResponse(TaskSummaryDto d) =>
        new(d.Id, d.Status.ToString(), d.Progress, d.Dimension, d.CreatedAt, d.CompletedAt);

    public static TaskDetailsResponse ToResponse(TaskDetailsDto d) =>
        new(d.Id, d.Status.ToString(), d.Progress, d.Dimension, d.CreatedAt, d.CompletedAt,
            ParseJson(d.ResultJson), d.ErrorMessage);

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone(); 
    }
}