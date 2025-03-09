using Avalonia;
using Avalonia.Controls;
using System.Linq;

namespace VTACheckClock.Helpers
{
    public static class WindowHelper
    {
        /// <summary>
        /// Centra una ventana en la segunda pantalla si hay múltiples pantallas disponibles.
        /// Si solo hay una pantalla, la centra en la pantalla principal.
        /// </summary>
        /// <param name="window">La ventana a centrar</param>
        /// <param name="screenIndex">Índice de la pantalla (0 = primera pantalla, 1 = segunda pantalla, etc.)</param>
        public static void CenterOnScreen(Window window, int screenIndex = 1)
        {
            // Asegurarse que la ventana está inicializada
            if (window.PlatformImpl == null) {
                // Si la ventana no está inicializada, conectarse al evento Opened
                window.Opened += (s, e) => CenterWindowOnScreen(window, screenIndex);
            } else {
                // Si la ventana ya está inicializada, centrarla inmediatamente
                CenterWindowOnScreen(window, screenIndex);
            }
        }

        private static void CenterWindowOnScreen(Window window, int screenIndex)
        {
            // Acceder a las pantallas a través de la aplicación actual
            var screens = window.Screens.All.ToList();

            // Verificar si el índice de pantalla solicitado es válido
            if (screens.Count > screenIndex)
            {
                var wHeight = window.Height;
                // Obtener la pantalla solicitada
                var targetScreen = screens[screenIndex];
                var xWidth = (targetScreen.Bounds.Width - window.Width) / 2;
                var xHeight = (targetScreen.Bounds.Height - wHeight) / 2;

                // Calcular la posición central en la pantalla seleccionada
                double left = targetScreen.Bounds.X + xWidth;
                double top = targetScreen.Bounds.Y + xHeight;

                // Establecer la posición de la ventana
                window.Position = new PixelPoint((int)left, (int)top);
            }
            else {
                // Si no hay suficientes pantallas, centrar en la pantalla principal
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }
    }
}
