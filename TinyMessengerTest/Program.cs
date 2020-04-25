using System;
using System.Threading;

using TinyMessenger;

namespace TinyMessengerTest
{
    class Program
    {
        static TinyMessengerHub messenger = new TinyMessengerHub();
        static void Main(string[] args)
        {

            messenger.Subscribe<MyMessage>(m => { Console.WriteLine("recieved"); }) ;
            messenger.Publish(new MyMessage());
            Console.WriteLine("Hello World!");
            Timer t = new Timer(TimerCallback, null, 0, 2000);
            Console.ReadLine();
        }

        private static void TimerCallback(Object o)
        { 
            messenger.Publish(new MyMessage());
        }

    }

    class MyMessage : ITinyMessage
    {
        public object Sender { get; private set; }
    }

}

