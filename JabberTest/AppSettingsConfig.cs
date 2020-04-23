using System;
using System.Collections.Generic;
using System.Text;

namespace JabberTest
{
    public class AppSettingsConfig
    {
        public string ShoreTelToken { get; set; }
        public string Host { get; set; }
        public int Port { get; set; }
        public string UserName { get; set; }
        public string Domain { get; set; }

        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; }

    }
}
