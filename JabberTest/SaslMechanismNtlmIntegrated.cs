using System;
using System.Collections.Generic;
using System.Text;
using MailKit.Security;
using NSspi;
using NSspi.Contexts;
using NSspi.Credentials;


namespace JabberTest
{

    /// <summary>
    /// The NTLM Integrated Auth SASL mechanism.
    /// </summary>
    /// <remarks>
    /// A SASL mechanism based on NTLM using the credentials of the current user 
    /// via Windows Integrated Authentication (SSPI).
    /// </remarks>
    public class SaslMechanismNtlmIntegrated : SaslMechanism
    {
        enum LoginState
        {
            Initial,
            Challenge
        }

        LoginState state;
        ClientContext sspiContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="MailKit.Security.SaslMechanismNtlmIntegrated"/> class.
        /// </summary>
        /// <remarks>
        /// Creates a new NTLM Integrated Auth SASL context.
        /// </remarks>
        public SaslMechanismNtlmIntegrated() : base(string.Empty, string.Empty)
        {
        }

        /// <summary>
        /// Gets the name of the mechanism.
        /// </summary>
        /// <remarks>
        /// Gets the name of the mechanism.
        /// </remarks>
        /// <value>The name of the mechanism.</value>
        public override string MechanismName
        {
            get { return "NTLM"; }
        }

        /// <summary>
        /// Gets whether or not the mechanism supports an initial response (SASL-IR).
        /// </summary>
        /// <remarks>
        /// SASL mechanisms that support sending an initial client response to the server
        /// should return <value>true</value>.
        /// </remarks>
        /// <value><c>true</c> if the mechanism supports an initial response; otherwise, <c>false</c>.</value>
        public override bool SupportsInitialResponse
        {
            get { return true; }
        }

        /// <summary>
        /// Parses the server's challenge token and returns the next challenge response.
        /// </summary>
        /// <remarks>
        /// Parses the server's challenge token and returns the next challenge response.
        /// </remarks>
        /// <returns>The next challenge response.</returns>
        /// <param name="token">The server's challenge token.</param>
        /// <param name="startIndex">The index into the token specifying where the server's challenge begins.</param>
        /// <param name="length">The length of the server's challenge.</param>
        /// <exception cref="System.InvalidOperationException">
        /// The SASL mechanism is already authenticated.
        /// </exception>
        /// <exception cref="SaslException">
        /// An error has occurred while parsing the server's challenge token.
        /// </exception>
        protected override byte[] Challenge(byte[] token, int startIndex, int length)
        {
            if (IsAuthenticated)
                throw new InvalidOperationException();

            InitializeSSPIContext();

            byte[] serverResponse = null;
            SecurityStatus status;

            switch (state)
            {
                case LoginState.Initial:
                    status = sspiContext.Init(null, out serverResponse);
                    state = LoginState.Challenge;
                    break;
                case LoginState.Challenge:
                    status = sspiContext.Init(token, out serverResponse);
                    IsAuthenticated = true;
                    break;
                default:
                    throw new IndexOutOfRangeException("state");
            }

            return serverResponse;
        }

        private void InitializeSSPIContext()
        {
            if (sspiContext != null)
            {
                return;
            }

            var credential = new ClientCurrentCredential(PackageNames.Ntlm);

            sspiContext = new ClientContext(
                credential,
                string.Empty,
                ContextAttrib.InitIntegrity |
                ContextAttrib.ReplayDetect |
                ContextAttrib.SequenceDetect |
                ContextAttrib.Confidentiality);
        }

        /// <summary>
        /// Resets the state of the SASL mechanism.
        /// </summary>
        /// <remarks>
        /// Resets the state of the SASL mechanism.
        /// </remarks>
        public override void Reset()
        {
            state = LoginState.Initial;
            base.Reset();
        }
    }
}
