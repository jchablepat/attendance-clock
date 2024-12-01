using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using VTACheckClock.Services.Libs;

namespace VTACheckClock.Services
{
    public class FolderPickerService
    {
        public static async Task<IStorageFolder?> GetStartLocationAsync(IStorageProvider storageProvider, string? PathTmp)
        {
            if (Directory.Exists(PathTmp))
            {
                return await storageProvider.TryGetFolderFromPathAsync(PathTmp);
            }
            else
            {
                return await storageProvider.TryGetFolderFromPathAsync(GlobalVars.DefWorkPath);
            }
        }

        public static async Task<string> OpenFolderBrowser()
        {
            var pathTmp = GlobalVars.DefWorkPath;
            var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime).Windows.LastOrDefault();
            var topLevel = TopLevel.GetTopLevel(window);

            if (topLevel != null) {
                var folderOptions = new FolderPickerOpenOptions
                {
                    Title = "Seleccione un Directorio",
                    SuggestedStartLocation = await FolderPickerService.GetStartLocationAsync(topLevel.StorageProvider, pathTmp)
                };

                var result = await topLevel.StorageProvider.OpenFolderPickerAsync(folderOptions);

                if (result.Count > 0) {
                    pathTmp = result[0].Path.LocalPath;
                }
            }

            return pathTmp;
        }
    }
}
