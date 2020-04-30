using System;
using System.Collections.Generic;
using System.Text;

namespace JabberTest
{
    class Email
    {
        public StringBuilder mailBody = new StringBuilder();
        public string EmailFrom { get; set; }
        public DateTime LastAccess { get; private set; }

        public string Thread { get;  }

        public TimeSpan GetSpanFromLastAccess()
        {
            return DateTime.Now.Subtract(LastAccess);
        } 
        public void AddString(string newLine)
        {
            mailBody.AppendLine(newLine);
            LastAccess = DateTime.Now;
        }

        public Email(string emailFrom, string thread)
        {
            EmailFrom = emailFrom;
            LastAccess = DateTime.Now;
            Thread = thread;
        }
    }
}
