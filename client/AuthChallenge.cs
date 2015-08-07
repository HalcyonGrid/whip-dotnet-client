using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;

namespace InWorldz.Whip.Client
{
    public class AuthChallenge
    {
        public const byte PACKET_IDENTIFIER = 0;
        public const ushort MESSAGE_SIZE = 8;
        public const ushort CHALLENGE_SIZE = 7;

        private AppendableByteArray _challenge = new AppendableByteArray(CHALLENGE_SIZE);

        public AuthChallenge(Socket conn)
        {
            int soFar = 0;
            byte[] buffer = new byte[MESSAGE_SIZE];

            while (soFar < MESSAGE_SIZE)
            {
                //read the header and challenge phrase
                int rcvd = conn.Receive(buffer, (int)MESSAGE_SIZE - soFar, SocketFlags.None);
                if (rcvd == 0) throw new AuthException("Disconnect during authentication");
                if (soFar == 0 && buffer[0] != PACKET_IDENTIFIER) throw new AuthException("Invalid challenge packet header");

                //skip the first byte
                if (soFar == 0)
                {
                    if (rcvd > 1) _challenge.Append(buffer, 1, rcvd - 1);
                }
                else
                {
                    _challenge.Append(buffer, 0, rcvd);
                }

                soFar += rcvd;
            }
        }

        public byte[] Challenge
        {
            get
            {
                return _challenge.data;
            }
        }
    }
}
