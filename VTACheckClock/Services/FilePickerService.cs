using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System.Threading.Tasks;

namespace VTACheckClock.Services
{
    public interface IFilePickerService
    {
        Task<IStorageFile?> OpenFilePickerAsync();
    }

    public class FilePickerService : IFilePickerService
    {
        public static FilePickerFileType ImageAll { get; } = new("All Images")
        {
            Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp"],
            AppleUniformTypeIdentifiers = ["public.image"],
            MimeTypes = ["image/*"]
        };

        public async Task<IStorageFile?> OpenFilePickerAsync()
        {
            var MainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var windows = MainWindow?.OwnedWindows;

            IStorageProvider? provider = MainWindow?.StorageProvider;

            if (windows?.Count > 0) {
                provider = windows[0].StorageProvider;
            }

            if(provider == null) return null;

            var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions() {
                Title = "Seleccionar archivo de conexión",
                AllowMultiple = false,
                FileTypeFilter = [
                    new("Archivos Hash") { 
                        Patterns = ["*.hash"] 
                    }
                    //FilePickerFileTypes.ImageAll, 
                    //FilePickerFileTypes.Pdf
                ]
            });

            return files?.Count >= 1 ? files[0] : null;
        }
    }
}
