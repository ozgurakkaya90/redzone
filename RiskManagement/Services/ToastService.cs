namespace RiskManagement.Services;

public enum ToastType { Success, Error, Info, Warning }

public record Toast(string Message, ToastType Type, int DurationMs = 3000);

public class ToastService
{
    public event Action<Toast>? OnShow;

    public void Success(string message, int ms = 2800) => OnShow?.Invoke(new Toast(message, ToastType.Success, ms));
    public void Error(string message,   int ms = 6000) => OnShow?.Invoke(new Toast(message, ToastType.Error,   ms));
    public void Info(string message,    int ms = 3500) => OnShow?.Invoke(new Toast(message, ToastType.Info,    ms));
    public void Warning(string message, int ms = 4500) => OnShow?.Invoke(new Toast(message, ToastType.Warning, ms));
}
