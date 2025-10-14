using System.Threading.Tasks;

namespace PigPicPot.Services
{
    public interface ISettingsService
    {
        Task SavePinState(bool isPinned);
        Task<bool> LoadPinState();
    }
}
