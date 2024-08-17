using System;
using System.Collections.Generic;
using System.Text;

namespace CapFrameX.Webservice.Data.Options
{
    public class SmtpOptions
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool UseSTARTTLS { get; set; }
        public string[] Recipients { get; set; }
        public string From { get; set; }
    }
}
