using System;
using System.Collections.Generic;
using System.Text;

namespace JabberTest
{
    class Email
    {
        public StringBuilder mailBody = new StringBuilder();
        public string EmailFrom { get; set; }
        public void AddString(string newLines)
        {
            mailBody.Append(newLines);
        }

        public Email(string emailFrom)
        {
            EmailFrom = emailFrom;
        }
    }
}
