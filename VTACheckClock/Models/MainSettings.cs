namespace VTACheckClock.Models
{
    public class MainSettings
    {
        public string? Ws_url { get; set; }
        public string? Db_server { get; set; }
        public string? Db_name { get; set; }
        public string? Db_user { get; set; }
        public string? Db_pass { get; set; }
        public string? Ftp_url { get; set; }
        public string? Ftp_port { get; set; }
        public string? Ftp_user { get; set; }
        public string? Ftp_pass { get; set; }
        public string? Path_tmp { get; set; }
        public string? Logo { get; set; }
        public string? Employees_host { get; set; }
        public bool Websocket_enabled { get; set; }
        public string? Websocket_host { get; set; }
        public string? Websocket_port { get; set; }
        public string? Pusher_app_id { get; set; }
        public string? Pusher_key { get; set; }
        public string? Pusher_secret { get; set; }
        public string? Pusher_cluster { get; set; }
        public string? Event_name { get; set; }
        public bool MailEnabled { get; set; }
        public string? MailServer { get; set; }
        public string? MailPort { get; set; }
        public string? MailUser { get; set; }
        public string? MailPass { get; set; }
        public string? MailRecipient { get; set; }
        public bool UsePusher { get; set; }
        public string? SignalRHubUrl { get; set; }
        public string? SignalRHubName { get; set; }
        public string? SignalRMethodName { get; set; }
        public string? SignalRApiKey { get; set; }

        // Seq logging configuration
        public string? SeqUrl { get; set; }
        public string? SeqApiKey { get; set; }
    }
}
