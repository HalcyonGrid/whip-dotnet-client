using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace InWorldz.Whip.Client
{
    /// <summary>
    /// Request from the client to the server
    /// </summary>
    public class ClientRequestMsg
    {
        private const short HEADER_SIZE = 37;
        private const short UUID_TAG_LOCATION = 1;

        private AppendableByteArray _data;

        /// <summary>
        /// Type of request
        /// </summary>
        public enum RequestType
        {
            GET = 10,
            PUT = 11,
            PURGE = 12,
            TEST = 13,
            MAINT_PURGELOCALS = 14,
            STATUS_GET = 15,
            STORED_ASSET_IDS_GET = 16,
            GET_DONTCACHE = 17,
        }

        public RequestType Type
        {
            get
            {
                return (RequestType)_data.data[0];
            }
        }

        /// <summary>
        /// Sets up the data in the header
        /// </summary>
        /// <param name="type"></param>
        /// <param name="assetUUID"></param>
        /// <param name="dataSize"></param>
        private void SetupHeader(RequestType type, string assetUUID, int dataSize)
        {
            _data = new AppendableByteArray(HEADER_SIZE + dataSize);
            _data.Append((byte)type);
            _data.Append(Util.UuidToAscii(assetUUID));
            _data.Append(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(dataSize))); //size
        }

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="type"></param>
        /// <param name="assetUUID"></param>
        public ClientRequestMsg(RequestType type, string assetUUID)
        {
            this.SetupHeader(type, assetUUID, 0);
        }

        /// <summary>
        /// CTOR
        /// </summary>
        /// <param name="type"></param>
        /// <param name="assetUUID"></param>
        /// <param name="data"></param>
        public ClientRequestMsg(RequestType type, string assetUUID, byte[] data)
        {
            this.SetupHeader(type, assetUUID, data.Length);
            _data.Append(data);
        }

        public string GetUuid()
        {
            return Util.BytesToUuid(_data, UUID_TAG_LOCATION);
        }

        public void Send(Socket conn)
        {
            int amtSent = 0;

            while (amtSent < _data.Length)
            {
                amtSent += conn.Send(_data.data, amtSent, _data.Length - amtSent, SocketFlags.None);
            }
        }
    }
}
