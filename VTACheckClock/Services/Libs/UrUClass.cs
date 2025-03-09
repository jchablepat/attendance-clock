using Avalonia.Controls;
using DPUruNet;
using NLog;
using System;
using System.Diagnostics;
using System.Threading;
using static VTACheckClock.Views.MessageBox;

namespace VTACheckClock.Services.Libs
{
    class UrUClass
    {
        /// <summary>
        /// Reset the UI causing the user to reselect a reader.
        /// </summary>
        public static bool Reset { get; set; }
        public static Reader? CurrentReader { get; set; }
        private static ReaderCollection? _readers;
        public static Logger Log = LogManager.GetLogger("app_logger");

        /// <summary>
        /// Obtiene los lectores de huellas disponibles y asigna la instancia del primero por defecto
        /// </summary>
        public static void LoadCurrentReader()
        {
            try {
                _readers = ReaderCollection.GetReaders();
                foreach (Reader Reader in _readers)
                {
                    var reader_name = Reader.Description.Name;
                }

                if (CurrentReader != null) {
                    CurrentReader.Dispose();
                    CurrentReader = null;
                }

                CurrentReader = _readers[0];
            } catch {}
        }

        /// <summary>
        /// Inicializa el dispositivo de lectura de huella dactilar y verifica el estado del mismo.
        /// </summary>
        /// <returns>True si la operación fue exitosa.</returns>
        public static bool OpenReader()
        {
            if(CurrentReader == null) return false;

            using Tracer tracer = new("UrUClass::OpenReader");
            Reset = false;
            Constants.ResultCode result = Constants.ResultCode.DP_DEVICE_FAILURE;
            result = CurrentReader.Open(Constants.CapturePriority.DP_PRIORITY_COOPERATIVE);

            if (result != Constants.ResultCode.DP_SUCCESS)
            {
                Show(null, "Lector de Huella", "El dispositivo respondió con el error:  " + result, MessageBoxButtons.Ok);
                Reset = true;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Asocia el manejador del evento de captura e inicia el modo de captura asíncrona.
        /// </summary>
        /// <param name="OnCaptured">Delegado para el manejo de la llamada CallBack del evento On_Captured.</param>
        /// <returns>True si la operación fue exitosa.</returns>
        public static bool StartCaptureAsync(Reader.CaptureCallback OnCaptured)
        {
            if (CurrentReader == null) return false;

            using Tracer tracer = new("UrUClass::StartCaptureAsync");
            CurrentReader.On_Captured += new Reader.CaptureCallback(OnCaptured);

            if (!CaptureFingerAsync())
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Cancela la captura de la huella y luego cierra el lector.
        /// </summary>
        /// <param name="OnCaptured">Delegado para el manejo de la llamada CallBack del evento On_Captured.</param>
        public static void CancelCaptureAndCloseReader(Reader.CaptureCallback? OnCaptured = null)
        {
            try {
                using Tracer tracer = new("UrUClass::CancelCaptureAndCloseReader");
                if (CurrentReader != null)
                {
                    CurrentReader.CancelCapture();
                    CurrentReader.Dispose();

                    if (Reset)
                    {
                        CurrentReader = null;
                    }
                }
            } catch (Exception) {}
        }

        /// <summary>
        /// Verifica el estado del dispositivo y toma acciones correctivas de ser necesario.
        /// </summary>
        public static void GetStatus()
        {
            using (Tracer tracer = new Tracer("UrUClass::GetStatus"))
            {
                Constants.ResultCode result = CurrentReader.GetStatus();

                if (result != Constants.ResultCode.DP_SUCCESS)
                {
                    Reset = true;
                    throw new Exception("" + result);
                }

                if (CurrentReader.Status.Status == Constants.ReaderStatuses.DP_STATUS_BUSY)
                {
                    Log.Warn("Lector ocupado... esperando 50 milisegundos.");
                    Thread.Sleep(50);
                }
                else if (CurrentReader.Status.Status == Constants.ReaderStatuses.DP_STATUS_NEED_CALIBRATION)
                {
                    Log.Warn("Calibrando lector.");
                    CurrentReader.Calibrate();
                }
                else if (CurrentReader.Status.Status != Constants.ReaderStatuses.DP_STATUS_READY)
                {
                    throw new Exception("Reader Status - " + CurrentReader.Status.Status);
                }
            }
        }

        /// <summary>
        /// Comprueba la calidad de la huella dactilar capturada.
        /// </summary>
        /// <param name="captureResult">Información de la captura de la huella dactilar.</param>
        /// <returns>True si la calidad de la captura es suficientemente aceptable.</returns>
        public static bool CheckCaptureResult(CaptureResult captureResult)
        {
            using (Tracer tracer = new("UrUClass::CheckCaptureResult"))
            {
                if (captureResult.Data == null || captureResult.ResultCode != Constants.ResultCode.DP_SUCCESS)
                {
                    if (captureResult.ResultCode != Constants.ResultCode.DP_SUCCESS)
                    {
                        Reset = true;
                        throw new Exception(captureResult.ResultCode.ToString());
                    }

                    // Send message if quality shows fake finger
                    if (captureResult.Quality != Constants.CaptureQuality.DP_QUALITY_CANCELED)
                    {
                        throw new Exception("Quality - " + captureResult.Quality);
                    }

                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Captura la huella dactilar, tras comprobar el estado del dispositivo e interceptar los posibles errores.
        /// </summary>
        /// <returns>True si la operación fue exitosa.</returns>
        public static bool CaptureFingerAsync()
        {
            using (Tracer tracer = new Tracer("UrUClass::CaptureFingerAsync"))
            {
                try
                {
                    GetStatus();
                    Constants.ResultCode captureResult = CurrentReader.CaptureAsync(Constants.Formats.Fid.ISO, Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT, CurrentReader.Capabilities.Resolutions[0]);

                    if (captureResult != Constants.ResultCode.DP_SUCCESS)
                    {
                        Reset = true;
                        throw new Exception("" + captureResult);
                    }

                    return true;
                } catch(Exception ex) {
                    Log.Warn("Error del Lector de huella en el evento <CaptureFingerAsync>:  " + ex.Message);
                    //Show(null, "Lector de Huella", "Error:  " + ex.Message, MessageBoxButtons.Ok);
                    return false;
                }
            }
        }

        public static void ControlControls(int la_action, Control el_control, object? la_payload = null)
        {
            try {
                //    if (el_control.InvokeRequired)
                //    {
                //        ControlControlsCallback d = new ControlControlsCallback(ControlControls);
                //        el_control.Invoke(d, new object[] { la_action, el_control, la_payload });
                //    }

                //    else
                //    {
                //        switch (la_action)
                //        {
                //            case 0:
                //                ((PictureBox)el_control).Image = ((Bitmap)la_payload);
                //                ((PictureBox)el_control).Refresh();
                //                break;

                //            case 1:
                //                ((ProgressBar)el_control).Value = (int)la_payload;
                //                break;

                //            case 2:
                //                ((Label)el_control).Text = ((string)la_payload);
                //                break;

                //            case 3:
                //                ((Form)el_control).DialogResult = DialogResult.OK;
                //                ((Form)el_control).Close();
                //                break;

                //            case 4:
                //                ((Button)el_control).Visible = true;
                //                ((Button)el_control).Enabled = true;
                //                ((Button)el_control).PerformClick();
                //                ((Button)el_control).Visible = false;
                //                ((Button)el_control).Enabled = false;
                //                break;
                //        }
                //    }
            } catch (Exception) {}
        }
    }
}
