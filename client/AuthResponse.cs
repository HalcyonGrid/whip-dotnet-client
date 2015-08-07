using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using System.Net.Sockets;

namespace InWorldz.Whip.Client
{
    /// <summary>
    /// Response to an authentication challenge from the server
    /// </summary>
    public class AuthResponse
    {
        public const ushort MESSAGE_SIZE = 41;
        public const byte PACKET_IDENTIFIER = 0;

        private AppendableByteArray _rawMessageData = new AppendableByteArray(MESSAGE_SIZE);

        /// <summary>
        /// Constructs a new authentication response
        /// </summary>
        /// <param name="challenge">The challenge from the server</param>
        /// <param name="password">The password for the server</param>
        public AuthResponse(AuthChallenge challenge, string password)
        {
            //convert the password to ascii
            ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] asciiPW = encoding.GetBytes(password);

            //get data from the challenge
            byte[] challengeBytes = challenge.Challenge;

            //add the two ranges together and compute the hash
            AppendableByteArray authString = new AppendableByteArray(asciiPW.Length + challengeBytes.Length);
            authString.Append(asciiPW);
            authString.Append(challengeBytes);

            SHA1 sha = new SHA1CryptoServiceProvider();
            byte[] challengeHash = sha.ComputeHash(authString.data);

            //copy the results to the raw packet data
            _rawMessageData.Append(PACKET_IDENTIFIER);
            _rawMessageData.Append(encoding.GetBytes(Util.HashToHex(challengeHash)));
        }

        /// <summary>
        /// Sends the response over the given connection
        /// </summary>
        /// <param name="conn">A connected socket</param>
        public void Send(Socket conn)
        {
            int amtSent = 0;

            while (amtSent < _rawMessageData.data.Length)
            {
                amtSent += conn.Send(_rawMessageData.data, amtSent, _rawMessageData.data.Length - amtSent, SocketFlags.None);
            }
        }
    }
}
