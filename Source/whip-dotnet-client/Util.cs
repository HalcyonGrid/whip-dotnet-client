using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace InWorldz.Whip.Client
{
    public class Util
    {
        /// <summary>
        /// Length of the UUID byte array
        /// </summary>
        private const short UUID_LEN = 32;

        /// <summary>
        /// Removes dashes from the uuid
        /// </summary>
        /// <param name="uuid"></param>
        public static string FixUuid(string uuid)
        {
            return uuid.Replace("-", "");
        }

        /// <summary>
        /// Converts a UUID string to a byte array in ASCII
        /// </summary>
        /// <param name="uuid">The uuid string to convert</param>
        /// <returns>An ascii byte array with the hex uuid</returns>
        public static byte[] UuidToAscii(string uuid)
        {
            uuid = Util.FixUuid(uuid);

            ASCIIEncoding encoding = new ASCIIEncoding();
            return encoding.GetBytes(uuid);
        }

        /// <summary>
        /// Returns the Uuid from a series of bytes
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public static string BytesToUuid(AppendableByteArray bytes, int offset)
        {
            byte[] uuid = bytes.GetSubarray(offset, UUID_LEN);

            ASCIIEncoding encoding = new ASCIIEncoding();
            return encoding.GetString(uuid);
        }

        /// <summary>
        /// Network to host long.  Given an array of bytes converts to a host long
        /// </summary>
        /// <param name="bytes">Array of bytes from the network</param>
        /// <param name="offset">offset in the array to start looking</param>
        /// <returns>A host endian 32 bit number</returns>
        public static int NTOHL(byte[] bytes, int offset)
        {
            return IPAddress.NetworkToHostOrder(BitConverter.ToInt32(bytes, offset));
        }

        /// <summary>
        /// Host to network long.  Given a 32 bit integer, converts to an array of bytes
        /// </summary>
        /// <param name="number">The number to convert</param>
        /// <returns>An array of bytes in network order</returns>
        public static byte[] HTONL(int number)
        {
            return BitConverter.GetBytes(IPAddress.HostToNetworkOrder(number));
        }

        /// <summary>
        /// Converts a hash value to hex
        /// </summary>
        /// <param name="challengeHash"></param>
        /// <returns></returns>
        public static string HashToHex(byte[] challengeHash)
        {
            StringBuilder hexChallengeHash = new StringBuilder();
            foreach (byte n in challengeHash)
            {
                hexChallengeHash.Append(n.ToString("x2"));
            }

            return hexChallengeHash.ToString();
        }

        public static String LimitByteLength2(String input, Int32 maxLength)
        {
            for (Int32 i = input.Length - 1; i >= 0; i--)
            {
                if (Encoding.UTF8.GetByteCount(input.Substring(0, i + 1)) <= maxLength)
                {
                    return input.Substring(0, i + 1);
                }
            }

            return String.Empty;
        }
    }
}
