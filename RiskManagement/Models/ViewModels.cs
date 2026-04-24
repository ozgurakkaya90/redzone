namespace RiskManagement.Models;

public record EvalResult(string EvalType, double Probability, double Exposure, double Consequence, string? Notes);
