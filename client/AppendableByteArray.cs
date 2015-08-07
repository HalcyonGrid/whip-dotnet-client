using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;

namespace InWorldz.Whip.Client
{
    /// <summary>
    /// Holds a fixed sized array and keeps tract of an insert position.  Class created to prevent
    /// unnecessary array instantiations
    /// </summary>
    public class AppendableByteArray
    {
        private int _insertPos = 0;

        public byte[] data;


        public int Length
        {
            get
            {
                return data.Length;
            }
        }

        /// <summary>
        /// Creates an array with a fixed size as given
        /// </summary>
        /// <param name="size">Fixed size for this array</param>
        public AppendableByteArray(int size)
        {
            data = new byte[size];
        }

        
        /// <summary>
        /// Appends the given byte to the array
        /// </summary>
        /// <param name="moreData">A single byte to append</param>
        public void Append(byte moreData)
        {
            this.data[_insertPos++] = moreData;
        }

        /// <summary>
        /// Appends the given data to this array at the current position
        /// </summary>
        /// <param name="moreData">The array to append</param>
        public void Append(byte[] moreData)
        {
            if (moreData.Length > 0)
            {
                Array.Copy(moreData, 0, this.data, _insertPos, moreData.Length);
                _insertPos += moreData.Length;
            }
        }

        /// <summary>
        /// Appends starting at srcIndex of moreData to the end of the array
        /// </summary>
        /// <param name="moreData"></param>
        /// <param name="srcIndex"></param>
        public void Append(byte[] moreData, int srcIndex)
        {
            if (moreData.Length - srcIndex > 0)
            {
                Array.Copy(moreData, srcIndex, this.data, _insertPos, moreData.Length - srcIndex);
                _insertPos += moreData.Length - srcIndex;
            }
        }

        /// <summary>
        /// Appends starting at srcIndex of moreData to the given length
        /// </summary>
        /// <param name="moreData"></param>
        /// <param name="srcIndex"></param>
        /// <param name="srcLength"></param>
        public void Append(byte[] moreData, int srcIndex, int srcLength)
        {
            if (srcLength > 0)
            {
                Array.Copy(moreData, srcIndex, this.data, _insertPos, srcLength);
                _insertPos += srcLength;
            }   
        }

        /// <summary>
        /// Reads data from a socket into this byte array
        /// </summary>
        /// <param name="conn">The socket to read from</param>
        /// <param name="size">The amound of data to read</param>
        public void AppendFromSocket(Socket conn, int size)
        {
            int soFar = 0;
            while (soFar < size)
            {
                int szRead = conn.Receive(this.data, _insertPos, size - soFar, SocketFlags.None);
                if (szRead == 0) throw new AssetServerError("Server disconnect during receive");

                soFar += szRead;
                _insertPos += szRead;
            }
        }

        /// <summary>
        /// Returns a portion of this array
        /// </summary>
        /// <param name="srcIndex">Index to begin the copy at</param>
        /// <param name="length">Length of data to copy from this array</param>
        public byte[] GetSubarray(int srcIndex, int length)
        {
            byte[] subArray = new byte[length];
            Array.Copy(this.data, srcIndex, subArray, 0, length);

            return subArray;
        }
    }
}
