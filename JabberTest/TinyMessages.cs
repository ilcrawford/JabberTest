using TinyMessenger;

using Matrix.Xmpp.Base;


namespace JabberTest
{
    class ActiveMessage : ITinyMessage
    {
        public object Sender { get; private set; }
        public Message message { get; set; }

        public ActiveMessage(Message msg)
        {
            message = msg;
        }
    }

    class GoneMessage : ITinyMessage
    {
        public object Sender { get; private set; }
        public string Thread { get; set; }

        public GoneMessage(string thread)
        {
            Thread = thread;
        }
    }
}