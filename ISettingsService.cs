using System.Threading.Tasks;

public interface ISettingsService
{
    Task SavePinState(bool isPinned);
    Task<bool> LoadPinState();
}