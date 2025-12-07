namespace YoFi.V3.Application.Dto;

public record TransactionEditDto(DateOnly Date, decimal Amount, string Payee);
