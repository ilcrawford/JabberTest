using Matrix;
using Matrix.Sasl;
using Matrix.Xml;
using Matrix.Xmpp.Sasl;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JabberTest
{
    
    class ShoretelSSOProcessor : IAuthenticate
    {
        public async Task<XmppXElement> AuthenticateAsync(Mechanisms mechanisms, XmppClient xmppClient, CancellationToken cancellationToken)
        {
            var authMessage = new Auth(Matrix.Xmpp.Sasl.SaslMechanism.Plain, GetMessage(xmppClient));

            return
                await xmppClient.SendAsync<Success, Failure>(authMessage, cancellationToken);
        }

        private string GetMessage(XmppClient xmppClient)
        {
            // NULL Username NULL Password
            //var sb = new StringBuilder();
            //sb.Append((char)0);
            //sb.Append(xmppClient.Username);
            //sb.Append((char)0);
            //sb.Append(xmppClient.Password);
            //byte[] msg = Encoding.UTF8.GetBytes(sb.ToString());
            return "AGljcmF3Zm9yZAB7InNlc3Npb24taWQiOiIwYTY0MjAwYjUxMjkwYzQyMjE1NDVjMDE3YWUzZjI1M2RlMjY5NGUxODRiNDg1YTkiLCJsb2dvbi11cmwiOiJodHRwOi8vMTAuMTAwLjMyLjExOjU0NDkvbWdtdC1hcGkiLCJicm93c2VyLWlwIjoiMTAuMTAwLjIwLjEzMSIsInVzZXItaWQiOiJpY3Jhd2ZvcmQiLCJ1c2VyLXJvbGUiOiJ1c2VyX3JvbGUifQ==";
        }
    }
}
