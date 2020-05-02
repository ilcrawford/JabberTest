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

using Serilog.Configuration;
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
        static readonly ServiceCollection serviceCollection = new ServiceCollection();
        static ServiceProvider serviceProvider;
        static Microsoft.Extensions.Logging.ILogger logger;
        static TinyMessengerHub msgHub;
        static readonly ConcurrentDictionary<string, Email> emailCollection = new ConcurrentDictionary<string, Email>();
        static readonly AppSettingsConfig appConfig = new AppSettingsConfig();
        static XmppClient xmppClient;

        /// <summary>
        /// Setup services for dependency injection
        /// </summary>
        /// <param name="services"></param>
        private static void ConfigureServices(IServiceCollection services)
        {
            var configBuilder = new ConfigurationBuilder()
             .SetBasePath(Directory.GetCurrentDirectory())
             .AddJsonFile("AppSettings.json", optional: true)
             .AddUserSecrets<Program>();
            var config = configBuilder.Build();
            config.GetSection("AppSettings").Bind(appConfig);

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(config)
                //.WriteTo.File("console.log")
                //.MinimumLevel.Verbose()
                .CreateLogger();

            services.AddLogging(configure => configure.AddSerilog())
                    .AddTransient<Program>();


        }

       
        static void Main(string[] args)
        {
            ConfigureServices(serviceCollection);

            serviceProvider = serviceCollection.BuildServiceProvider();

            logger = serviceProvider.GetService<ILogger<Program>>();

            logger.LogInformation($"Shortel Token {appConfig.ShoreTelToken}");
            logger.LogInformation($"Shoretel Host {appConfig.Host}");
            logger.LogInformation($"Shortel Domain {appConfig.Domain}");
            logger.LogInformation($"Shortel Username {appConfig.UserName}");
            logger.LogInformation($"Smtp Host {appConfig.SmtpHost}");
            logger.LogInformation($"Smtp port {appConfig.SmtpPort}");
            logger.LogInformation($"Smtp port {appConfig.SendEmailDelay}");
            foreach (string user in appConfig.UsersToRespondTo)
            {
                logger.LogInformation($"Users to respond to {user}");
            }

            msgHub = new TinyMessengerHub(new ErrorHandler(logger));

            msgHub.Subscribe<ActiveMessage>(m => 
            {
                logger.LogDebug("Enter ActiveMessage");
                try
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
                }
                catch (Exception e)
                {
                    logger.LogError($"Active message error {e.Message}");
                }
                logger.LogDebug("Exit ActiveMessage");
            });

            msgHub.Subscribe<GoneMessage>(m =>
           {
               logger.LogDebug("Enter GoneMessage");
               try
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
               }
               catch (Exception e)
               {
                   logger.LogError($"Gone message error {e.Message}");
               }
               logger.LogDebug("Exit GoneMessage");
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
                    logger.LogDebug("Enter Presence observer");
                    //Console.WriteLine(el.ToString());
                    //logger.LogInformation(el.ToString());
                    logger.LogDebug("Exit Presence observer");
                });

            xmppClient
                .XmppXElementStreamObserver
                .Where(el => el is Message)
                .Subscribe(el =>
                {
                    logger.LogDebug("Enter Message observer");
                    var msg = el as Message;

                    switch (msg.Chatstate)
                    {
                        case Matrix.Xmpp.Chatstates.Chatstate.Active:
                            msgHub.PublishAsync(new ActiveMessage(msg), x => { });
                            break;
                        case Matrix.Xmpp.Chatstates.Chatstate.Composing:
                            break;
                        case Matrix.Xmpp.Chatstates.Chatstate.Gone:
                            msgHub.PublishAsync(new GoneMessage(msg.Thread), x => { });
                            break;

                    }

                    logger.LogDebug("Exit Message observer");
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

            using (Timer t = new Timer(TimerCallback, null, 0, 1000))
            {
                Console.ReadLine();
            }

            // Disconnect the XMPP connection
            xmppClient.DisconnectAsync().GetAwaiter().GetResult();

        }

        private static void TimerCallback(Object o)
        {
            logger.LogDebug($"Enter TimerCallback");

           

            foreach (var email in emailCollection.Values )
            {
                
                Monitor.Enter(email);
                try
                {
                    logger.LogDebug($"Tick {email.GetSpanFromLastAccess().TotalMinutes}");
                    if (email.GetSpanFromLastAccess().TotalMinutes >= appConfig.SendEmailDelay )
                    {
                        msgHub.PublishAsync(new GoneMessage(email.Thread), x => { });
                    }
                }
                finally
                {
                    Monitor.Exit(email);
                }

            }

            logger.LogDebug($"Exit TimerCallback ");
        }

        class ErrorHandler: ISubscriberErrorHandler
        {
            private Microsoft.Extensions.Logging.ILogger logger;
            public ErrorHandler(Microsoft.Extensions.Logging.ILogger logger)
            {
                this.logger = logger;
            }
            public void Handle(ITinyMessage message, Exception exception)
            {
                logger.LogError($"TinyMessenger ERROR {message}, {exception.Message}");

            }

        }

    }


}

