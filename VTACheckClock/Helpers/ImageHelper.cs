using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using VTACheckClock.Services.Libs;

namespace VTACheckClock.Helpers
{
    public static class ImageHelper
    {
        public static Bitmap? LoadFromAvares(string destinationPath)
        {
            try {
                var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
                var pathAvaresProject = new Uri($"avares://{assemblyName}{destinationPath}");
                var bitmap = new Bitmap(AssetLoader.Open(pathAvaresProject));

                return bitmap;
            } catch(Exception) {
                return null;
            }
        }

        public static Bitmap LoadFromResource(Uri resourceUri)
        {
            return new Bitmap(AssetLoader.Open(resourceUri));
        }

        public static Bitmap? LoadFromResource(string destinationPath)
        {
            try {
                var stream = File.OpenRead($"{GlobalVars.AppWorkPath}{destinationPath}");
                var bitmap = new Bitmap(stream);

                return bitmap;
            } catch (Exception) {
                return null;
            }
        }

        public static async Task<Bitmap?> LoadFromWeb(Uri url)
        {
            using var httpClient = new HttpClient();
            try {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var data = await response.Content.ReadAsByteArrayAsync();
                return new Bitmap(new MemoryStream(data));
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"An error occurred while downloading image '{url}' : {ex.Message}");
                return null;
            }
        }
    }
}
