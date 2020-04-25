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
        private string _ShoretelToken = "";
        public ShoretelSSOProcessor(string shortelToken)
        {
            _ShoretelToken = shortelToken;
        }
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
            return _ShoretelToken;
        }
    }
}
