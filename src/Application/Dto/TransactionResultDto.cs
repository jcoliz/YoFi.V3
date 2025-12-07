namespace YoFi.V3.Application.Dto;

public record TransactionResultDto(Guid Key, DateOnly Date, decimal Amount, string Payee);
