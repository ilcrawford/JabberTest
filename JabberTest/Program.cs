using System;
using System.Diagnostics;
using System.Net;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using DotNetty.Transport.Channels;

using MailKit.Net.Smtp;
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
using MailKit.Security;
using System.IO;

// TODO - capture new threads to email later.
// TODO - Only respond on the first item for thread.
// TODO - Get hosts and ports
// DONE - moved magic valued to appsettings.json

namespace JabberTest
{
    class Program
    {
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
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var logger = serviceProvider.GetService<ILogger<Program>>();
//            var config = serviceProvider.GetService<IConfiguration>();


            var configBuilder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("AppSettings.json", optional: true)
               .AddUserSecrets<Program>();
            var config = configBuilder.Build();
            var appConfig = new AppSettingsConfig();
            config.GetSection("AppSettings").Bind(appConfig);

            

            logger.LogDebug($"Shortel Token {appConfig.ShoreTelToken}");
            logger.LogDebug($"Shoretel Host {appConfig.Host}");
            logger.LogDebug($"Shortel Domain {appConfig.Domain}");
            logger.LogDebug($"Shortel Username {appConfig.UserName}");
            logger.LogDebug($"Smtp Host {appConfig.SmtpHost}");
            logger.LogDebug($"Smtp port {appConfig.SmtpPort}");

            var pipelineInitializerAction = new Action<IChannelPipeline, ISession>((pipeline, session) =>
            {
                pipeline.AddFirst(new MyLoggingHandler(logger));
            });


            var xmppClient = new XmppClient()
            {
                Username = appConfig.UserName,
                XmppDomain = appConfig.Domain,
                SaslHandler = new ShoretelSSOProcessor(),
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
                    Console.WriteLine(el.ToString());
                    //logger.LogInformation(el.ToString());
                });

            xmppClient
                .XmppXElementStreamObserver
                .Where(el => el is Message)
                .Subscribe(el =>
                {
                    var msg = el as Message;

                    if (msg.Chatstate == Matrix.Xmpp.Chatstates.Chatstate.Composing)
                    {
                        //    var txtMsg = "I am unable to respond to ShoreTel IMs right now.  Leave a message, exit the conversation and I will get back with you,";
                        //    var sndMsg = new Matrix.Xmpp.Client.Message(msg.From, MessageType.Chat, txtMsg, "");

                        //    xmppClient.SendAsync(sndMsg).GetAwaiter().GetResult();
                    }

                    logger.LogInformation(el.ToString());

                    try
                    {
                        using (var client = new SmtpClient(new MailKit.ProtocolLogger("smtp.log", false)))
                        {

                            client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                            client.CheckCertificateRevocation = false;



                            client.Connect(appConfig.SmtpHost, appConfig.SmtpPort, SecureSocketOptions.StartTlsWhenAvailable);

                            var mailMsg = new MimeMessage();

                            mailMsg.From.Add(new MailboxAddress(appConfig.UserName));
                            mailMsg.To.Add(new MailboxAddress("icrawford@maxor.com"));

                            mailMsg.Subject = msg.From.Bare;

                            var builder = new BodyBuilder
                            {
                                TextBody = msg.Body
                            };


                            mailMsg.Body = builder.ToMessageBody();


                            client.Send(mailMsg);

                            client.Disconnect(true);
                        }

                    }
                    catch (Exception e)
                    {

                        logger.LogError(e.Message.ToString());
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

            Console.ReadLine();

            // Disconnect the XMPP connection
            xmppClient.DisconnectAsync().GetAwaiter().GetResult();

            Console.ReadLine();

        }

    }

    
}

