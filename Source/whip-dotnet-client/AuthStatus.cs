using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;

namespace InWorldz.Whip.Client
{
    class AuthStatus
    {
        public enum StatusType
        {
            AS_SUCCESS = 0,
            AS_FAILURE = 1
        }

        private const int MESSAGE_SIZE = 2;
        private const byte PACKET_IDENTIFIER = 1;

        private AppendableByteArray _data = new AppendableByteArray(MESSAGE_SIZE);

        public AuthStatus(Socket conn)
        {
            int soFar = 0;
            byte[] buffer = new byte[MESSAGE_SIZE];

            while (soFar < MESSAGE_SIZE)
            {
                //read the header and challenge phrase
                int rcvd = conn.Receive(buffer, MESSAGE_SIZE, SocketFlags.None);
                if (rcvd == 0) throw new AuthException("Disconnect during authentication");
                if (soFar == 0 && buffer[0] != PACKET_IDENTIFIER) throw new AuthException("Invalid challenge packet header");

                 _data.Append(buffer, 0, rcvd);
                soFar += rcvd;
            }
        }

        public StatusType Status
        {
            get
            {
                return (StatusType)_data.data[1];
            }
        }
    }
}
