namespace VTACheckClock.Models
{
    /// <summary>
    /// Clase para encapsular la información de inicio de sesión.
    /// </summary>
    class SessionData
    {
        public string? usrname { get; set; }
        public string? passwd { get; set; }
        public int accpriv { get; set; }
    }
}
