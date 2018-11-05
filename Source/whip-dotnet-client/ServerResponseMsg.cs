using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;


namespace InWorldz.Whip.Client
{
    /// <summary>
    /// The server's response to a request
    /// </summary>
    public class ServerResponseMsg
    {
        public enum Result
        {
            FOUND = 10,
            NOT_FOUND = 11,
            ERROR = 12,
            OK = 13
        }

        private const short HEADER_SIZE = 37;
        private const short UUID_TAG_LOC = 1;
        private const short DATA_SZ_TAG_LOC = HEADER_SIZE - 4;

        //48 MB max data size
        private const int MAX_DATA_SIZE = 50331648;

        private AppendableByteArray _header = new AppendableByteArray(HEADER_SIZE);
        private AppendableByteArray _data = null;

        /// <summary>
        /// Status of the call
        /// </summary>
        public Result Status
        {
            get
            {
                return (Result)_header.data[0];
            }
        }

        /// <summary>
        /// UUID of the asset
        /// </summary>
        public String AssetUUID
        {
            get
            {
                return Util.BytesToUuid(_header, UUID_TAG_LOC);
            }
        }

        /// <summary>
        /// Returns the error message for error returns
        /// </summary>
        public String ErrorMessage
        {
            get
            {
                ASCIIEncoding encoding = new ASCIIEncoding();
                return encoding.GetString(_data.data, 0, _data.Length);
            }
        }

        public AppendableByteArray Data
        {
            get
            {
                return _data;
            }
        }

        private string GetHeaderSummary()
        {
            int intStatus = (int)_header.data[0];
            int dataSize = Util.NTOHL(_header.data, DATA_SZ_TAG_LOC);
            return " Status: " + intStatus + " Size was: " + dataSize;
        }

        public ServerResponseMsg(Socket conn)
        {
            //read the header
            _header.AppendFromSocket(conn, HEADER_SIZE);

            int intStatus = (int)_header.data[0];
            if (intStatus < (int)Result.FOUND || intStatus > (int)Result.OK)
            {
                throw new AssetProtocolError("Invalid result type in server response: " + GetHeaderSummary());
            }

            //read the data
            int dataSize = Util.NTOHL(_header.data, DATA_SZ_TAG_LOC);
            if (dataSize > MAX_DATA_SIZE || dataSize < 0)
            {
                throw new AssetProtocolError("Returned data was too long in response: " + GetHeaderSummary());
            }

            _data = new AppendableByteArray(dataSize);
            _data.AppendFromSocket(conn, dataSize);
        }


    }
}
