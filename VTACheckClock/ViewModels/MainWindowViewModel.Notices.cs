using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VTACheckClock.Models;
using VTACheckClock.Services;
using VTACheckClock.Services.Libs;

namespace VTACheckClock.ViewModels
{
    partial class MainWindowViewModel
    {
        private readonly NoticeData def_not = new() { nottit = "", notmsg = "No se encontró ningún aviso para esta ubicación o no se pudieron cargar." };
        private readonly DispatcherTimer tmrNotices = new();
        private static List<NoticeData>? notices;
        private static int curr_not = 0;

        /// <summary>
        /// Recupera los avisos emitidos para la oficina actual.
        /// </summary>
        private async Task GetNotices()
        {
            List<NoticeData>? la_salida = [];

            try {
                if (!GlobalVars.BeOffline)
                {
                    var ScantRequest = new ScantRequest { 
                        Question = (GlobalVars.clockSettings.clock_office.ToString()) 
                    };

                    var result = await CommonProcs.GetOfficeNoticesAsync(ScantRequest);
                    List<NoticeData> my_nots = result;

                    if (my_nots != null)
                    {
                        foreach (NoticeData notice in my_nots)
                        {
                            notice.nottit = CommonProcs.Base64ToStr(notice.nottit);
                            notice.notmsg = CommonProcs.Base64ToStr(notice.notmsg);

                            la_salida.Add(notice);
                            Notices.Add(new Notice() {
                                id = notice.notid,
                                caption = notice.nottit,
                                body = notice.notmsg,
                                image = notice.notimg
                            });
                        }

                        await GlobalVars.AppCache.SaveNotices(la_salida);
                    }
                } 
                else {
                    la_salida = GlobalVars.AppCache.RetrieveNotices();
                }

                if (la_salida.Count < 1) la_salida.Add(def_not);
            } catch {
                la_salida = [def_not];
            }
            finally {
               notices = la_salida;
            }

            ParseNotice();
        }

        private async void AddNewNotice(Notice? notice)
        {
            await GetNotices();
        }

        /// <summary>
        /// Agrega un aviso al recuperar el registro en tiempo real desde el servidor de WebSocket
        /// </summary>
        public async Task SetNotice(NoticeData notice)
        {
            tmrNotices.Stop();
            tmrNotices.IsEnabled = false;

            await GetNotices();

            tmrNotices.Start();
            tmrNotices.IsEnabled = true;
            curr_not = 0;
        }

        /// <summary>
        /// Evento TICK del temporizador de los avisos.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TmrNotices_Tick(object? sender, EventArgs e)
        {
            ParseNotice();
        }

        /// <summary>
        /// Muestra la información del anuncio correspondiente al índice actual.
        /// </summary>
        private void ParseNotice()
        {
            try
            {
                NoticeData la_notice = notices[curr_not];
                bool has_text = !(string.IsNullOrWhiteSpace(la_notice.notmsg) || (la_notice.notmsg == "null"));
                bool has_image = !(string.IsNullOrWhiteSpace(la_notice.notimg) || (la_notice.notimg == "null"));

                if (has_image) {
                    NoticeImage = CommonProcs.Base64ToBitmap(la_notice.notimg ?? "");
                }

                if (has_text)
                {
                    NoticeBody = la_notice.notmsg ?? "";
                }

                NoticeTitle = la_notice.nottit ?? "";
                SetNextNotice();
                if (NoticeCollectionEmpty) tmrNotices.Stop();
            }
            catch (Exception ex)
            {
                log.Warn("Error while showing Notice: " + ex.Message);
            }
        }

        /// <summary>
        /// Establece el siguiente índice del ciclo de avisos.
        /// </summary>
        private static void SetNextNotice()
        {
            curr_not = ((curr_not + 1) >= notices.Count) ? 0 : (curr_not + 1);
        }
    }
}