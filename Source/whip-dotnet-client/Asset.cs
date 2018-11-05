using System;
using System.Collections.Generic;
using System.Text;

namespace InWorldz.Whip.Client
{
    /// <summary>
    /// An asset sent from the asset server
    /// </summary>
    public class Asset
    {
        /// <summary>
        /// Size of the packet header
        /// </summary>
        private const short HEADER_SIZE = 39;
        /// <summary>
        /// location of the type tag
        /// </summary>
        private const short TYPE_TAG_LOC = 32;
        /// <summary>
        /// location of the local tag
        /// </summary>
        private const short LOCAL_TAG_LOC = 33;
        /// <summary>
        /// Location of the temporary tag
        /// </summary>
        private const short TEMPORARY_TAG_LOC = 34;
        /// <summary>
        /// Location of the create time tag
        /// </summary>
        private const short CREATE_TIME_TAG_LOC = 35;
        /// <summary>
        /// Location of the size of the name field
        /// </summary>
        private const short NAME_SIZE_TAG_LOC = 39;

        private string _uuid;
        private byte _type;
        private bool _local;
        private bool _temporary;
        private int _createTime;
        private string _name;
        private string _description;
        private byte[] _data;


        public string Uuid
        {
            get
            {
                return _uuid;
            }
        }

        public byte Type
        {
            get
            {
                return _type;
            }
        }

        public bool Local
        {
            get
            {
                return _local;
            }
        }

        public bool Temporary
        {
            get
            {
                return _temporary;
            }
        }

        public int CreateTime
        {
            get
            {
                return _createTime;
            }
        }

        public string Name
        {
            get
            {
                return _name;
            }
        }

        public string Description
        {
            get
            {
                return _description;
            }
        }

        public byte[] Data
        {
            get
            {
                return _data;
            }
        }

        public Asset(string uuid, byte type, bool local, bool temporary, int createTime,
            string name, string description, byte[] data)
        {
            _uuid = Util.FixUuid(uuid);
            _type = type;
            _local = local;
            _temporary = temporary;
            _createTime = createTime;
            _name = Util.LimitByteLength2(name, 255);
            _description = Util.LimitByteLength2(description, 255);
            _data = data;
        }

        /// <summary>
        /// Initializes a new asset object with the given data
        /// </summary>
        /// <param name="dataSize"></param>
        public Asset(AppendableByteArray data)
        {
            _uuid = Util.BytesToUuid(data, 0);
            _type = data.data[TYPE_TAG_LOC];
            _local = data.data[LOCAL_TAG_LOC] == 1;
            _temporary = data.data[TEMPORARY_TAG_LOC] == 1;
            _createTime = Util.NTOHL(data.data, CREATE_TIME_TAG_LOC);

            //now the dynamic sized fields
            UTF8Encoding encoding = new UTF8Encoding();

            //the name field
            byte nameFieldSize = data.data[NAME_SIZE_TAG_LOC];
            if (nameFieldSize > 0)
            {
                _name = encoding.GetString(data.data, NAME_SIZE_TAG_LOC + 1, nameFieldSize);
            }
            else
            {
                _name = String.Empty;
            }

            //the description field
            int descSizeFieldLoc = NAME_SIZE_TAG_LOC + nameFieldSize + 1;
            byte descFieldSize = data.data[descSizeFieldLoc];
            if (descFieldSize > 0)
            {
                _description = encoding.GetString(data.data, descSizeFieldLoc + 1, descFieldSize);
            }
            else
            {
                _description = String.Empty;
            }

            //finally, get the location of the data and it's size
            int dataSizeFieldLoc = descSizeFieldLoc + descFieldSize + 1;
            int dataSize = Util.NTOHL(data.data, dataSizeFieldLoc);
            int dataLoc = dataSizeFieldLoc + 4;

            //create the array now so that it will be shared between all reqestors
            if (dataSize > 0)
            {
                _data = data.GetSubarray(dataLoc, dataSize);
            }
            else
            {
                _data = new byte[0];
            }
        }

        public AppendableByteArray Serialize()
        {
            UTF8Encoding encoding = new UTF8Encoding();

            byte[] nameBytes = encoding.GetBytes(_name);
            byte[] descBytes = encoding.GetBytes(_description);

            if (nameBytes.Length > 255)
            {
                throw new AssetProtocolError(String.Format("Serialized asset name would be too long after encoding {0} {1}",
                    _name, _uuid));
            }

            if (descBytes.Length > 255)
            {
                throw new AssetProtocolError(String.Format("Serialized asset description would be too long after encoding {0} {1}",
                    _description, _uuid));
            }

            //see the packet diagram to understand where the size calculation is coming from
            AppendableByteArray retArray
                = new AppendableByteArray(HEADER_SIZE + 1 + nameBytes.Length + 1 + descBytes.Length + 4 + _data.Length);

            retArray.Append(Util.UuidToAscii(_uuid));
            retArray.Append((byte)_type);
            retArray.Append((byte)(_local ? 1 : 0));
            retArray.Append((byte)(_temporary ? 1 : 0));
            retArray.Append(Util.HTONL(_createTime));
            
            retArray.Append((byte)nameBytes.Length);
            retArray.Append(nameBytes);
            
            retArray.Append((byte)descBytes.Length);
            retArray.Append(descBytes);

            retArray.Append(Util.HTONL(_data.Length));
            retArray.Append(_data);

            return retArray;
        }
    }
}
