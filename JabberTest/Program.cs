using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using DotNetty.Transport.Channels;

using MailKit.Net.Smtp;
using MailKit.Security;

using MimeKit;

using Matrix;
using Matrix.Extensions.Client.Presence;
using Matrix.Extensions.Client.Roster;
using Matrix.Network.Resolver;
using Matrix.Sasl;
using Matrix.Xml;
using Matrix.Xmpp;
using Matrix.Xmpp.Base;
using Matrix.Xmpp.Sasl;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.UserSecrets;

using Serilog.Extensions.Logging;
using Serilog;

using TinyMessenger;


// DONE - capture new threads to email later.
// DONE - Only respond on the first item for thread.
// DONE - Get hosts and ports
// DONE - moved magic valued to appsettings.json
// TODO - Add time span to wait for gone message, tweak timer frequency.

namespace JabberTest
{
    class Program
    {
        static ServiceCollection serviceCollection = new ServiceCollection();
        static ServiceProvider serviceProvider;
        static Microsoft.Extensions.Logging.ILogger logger;
        static TinyMessengerHub msgHub = new TinyMessengerHub();
        static ConcurrentDictionary<string, Email> emailCollection = new ConcurrentDictionary<string, Email>();
        static AppSettingsConfig appConfig = new AppSettingsConfig();
        static XmppClient xmppClient;

        /// <summary>
        /// Setup services for dependency injection
        /// </summary>
        /// <param name="services"></param>
        private static void ConfigureServices(IServiceCollection services)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File("console.log")
                .MinimumLevel.Verbose()
                .CreateLogger();

            services.AddLogging(configure => configure.AddSerilog())
                    .AddTransient<Program>();


        }

       
        static void Main(string[] args)
        {
            ConfigureServices(serviceCollection);

            serviceProvider = serviceCollection.BuildServiceProvider();

            logger = serviceProvider.GetService<ILogger<Program>>();
            
            var configBuilder = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("AppSettings.json", optional: true)
              .AddUserSecrets<Program>();
            var config = configBuilder.Build();
            config.GetSection("AppSettings").Bind(appConfig);

            logger.LogDebug($"Shortel Token {appConfig.ShoreTelToken}");
            logger.LogDebug($"Shoretel Host {appConfig.Host}");
            logger.LogDebug($"Shortel Domain {appConfig.Domain}");
            logger.LogDebug($"Shortel Username {appConfig.UserName}");
            logger.LogDebug($"Smtp Host {appConfig.SmtpHost}");
            logger.LogDebug($"Smtp port {appConfig.SmtpPort}");
            foreach (string user in appConfig.UsersToRespondTo)
            {
                logger.LogDebug($"Users to respond to {user}");
            }
            

            msgHub.Subscribe<ActiveMessage>(m => 
            {
                var email = emailCollection.GetOrAdd(m.message.Thread, e => new Email(m.message.From.Bare, m.message.Thread));
                Monitor.Enter(email);
                try
                {
                    if (email.mailBody.Length < 1)
                    {
                        if (appConfig.UsersToRespondTo.FindIndex(x => x.Equals(email.EmailFrom, StringComparison.OrdinalIgnoreCase)) >= 0)
                        {
                            var txtMsg = "I am unable to respond to ShoreTel IMs right now.  Leave a message, exit the conversation and I will get back with you,";
                            var sndMsg = new Matrix.Xmpp.Client.Message(m.message.From, MessageType.Chat, txtMsg, "");
                            xmppClient.SendAsync(sndMsg).GetAwaiter().GetResult();
                        }

                    }
                    email.AddString(m.message.Body);
                }
                finally
                {
                    Monitor.Exit(email);
                }

            });

            msgHub.Subscribe<GoneMessage>(m =>
           {
               Email email;
               if (emailCollection.TryRemove(m.Thread, out email))
               {
                   Monitor.Enter(email);
                   try
                   {
                       using (var client = new SmtpClient(new MailKit.ProtocolLogger("smtp.log", false)))
                       {

                           client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                           client.CheckCertificateRevocation = false;

                           client.ConnectAsync(appConfig.SmtpHost, appConfig.SmtpPort, SecureSocketOptions.StartTlsWhenAvailable).GetAwaiter().GetResult();

                           var mailMsg = new MimeMessage();

                           mailMsg.From.Add(new MailboxAddress(email.EmailFrom));
                           mailMsg.To.Add(new MailboxAddress(appConfig.UserName));

                           mailMsg.Subject = $"ShoreTel message from {email.EmailFrom}";

                           var builder = new BodyBuilder
                           {
                               TextBody = email.mailBody.ToString()
                           };

                           mailMsg.Body = builder.ToMessageBody();

                           client.SendAsync(mailMsg).GetAwaiter().GetResult();

                           client.DisconnectAsync(true).GetAwaiter().GetResult();
                       }
                   }
                   finally
                   {
                       Monitor.Exit(email);
                   }
               }
           });
            

            var pipelineInitializerAction = new Action<IChannelPipeline, ISession>((pipeline, session) =>
            {
                pipeline.AddFirst(new MyLoggingHandler(logger));
            });

            xmppClient = new XmppClient()
            {
                Username = appConfig.UserName,
                XmppDomain = appConfig.Domain,
                SaslHandler = new ShoretelSSOProcessor(appConfig.ShoreTelToken),
                // use a local server for dev purposes running
                // on a non standard XMPP port 5333
                HostnameResolver = new StaticNameResolver(IPAddress.Parse(appConfig.Host), appConfig.Port)
            };

            xmppClient.XmppSessionStateObserver.Subscribe(v =>
            {
                Console.WriteLine($"State changed: {v}");
                logger.LogInformation(v.ToString());
            });

            xmppClient
                .XmppXElementStreamObserver
                .Where(el => el is Presence)
                .Subscribe(el =>
                {
                    //Console.WriteLine(el.ToString());
                    //logger.LogInformation(el.ToString());
                });

            xmppClient
                .XmppXElementStreamObserver
                .Where(el => el is Message)
                .Subscribe(el =>
                {
                    var msg = el as Message;

                    switch (msg.Chatstate)
                    {
                        case Matrix.Xmpp.Chatstates.Chatstate.Active:
                            msgHub.Publish(new ActiveMessage(msg));
                            break;
                        case Matrix.Xmpp.Chatstates.Chatstate.Composing:
                            break;
                        case Matrix.Xmpp.Chatstates.Chatstate.Gone:
                            msgHub.Publish(new GoneMessage(msg.Thread));
                            break;

                    }


                });

            xmppClient
                .XmppXElementStreamObserver
                .Where(el => el is Iq)
                .Subscribe(el =>
                {
                    Console.WriteLine(el.ToString());
                    logger.LogInformation(el.ToString());
                });

            xmppClient.ConnectAsync().GetAwaiter().GetResult();

            // Send our presence to the server
            xmppClient.SendPresenceAsync(Show.Chat, "free for chat").GetAwaiter().GetResult();

            Timer t = new Timer(TimerCallback, null, 0, 1000);

            Console.ReadLine();

            // Disconnect the XMPP connection
            xmppClient.DisconnectAsync().GetAwaiter().GetResult();

            Console.ReadLine();

        }

        private static void TimerCallback(Object o)
        {
            foreach (var email in emailCollection.Values )
            {
                Monitor.Enter(email);
                try
                {
                    if (email.GetSpanFromLastAccess().Minutes >= appConfig.SendEmailDelay )
                    {
                        msgHub.Publish(new GoneMessage(email.Thread));
                    }
                }
                finally
                {
                    Monitor.Exit(email);
                }

            }

            
        }


    }


}

