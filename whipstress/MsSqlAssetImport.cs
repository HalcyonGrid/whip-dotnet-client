using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using InWorldz.Whip.Client;
using System.IO;

namespace whipstress
{
    class MsSqlAssetImport
    {
        SqlConnection _conn;
        RemoteServer _server;
        Guid _startAt;
        bool _stop = false;

        public MsSqlAssetImport(RemoteServer server, string connstring, Guid startAt)
        {
            _conn = new SqlConnection(connstring);
            _conn.Open();

            _server = server;
            _startAt = startAt;
        }

        public void Start()
        {
            System.Threading.Thread th = new System.Threading.Thread(new System.Threading.ThreadStart(Run));
            th.Start();
        }

        public void Stop()
        {
            _stop = true;
        }

        public void Run()
        {
            int i = 0;
            /*int count = 0;

            SqlCommand countCmd =
                    new SqlCommand(
                        "SELECT COUNT(*) as CNT " +
                        "FROM assets " +
                        "WHERE id >= @id ",
                        _conn);

            countCmd.Parameters.AddWithValue("@id", _startAt);

            countCmd.CommandType = CommandType.Text;
            count = Convert.ToInt32(countCmd.ExecuteScalar());
            countCmd.Dispose();
            */

            while (true)
            {
                SqlCommand cmd =
                        new SqlCommand(
                            "SELECT TOP 100 id, name, description, assetType, local, temporary, data, create_time " +
                            "FROM assets " +
                            "WHERE id > @id " +
                            "ORDER BY id",
                            _conn);

                cmd.Parameters.AddWithValue("@id", _startAt);

                try
                {
                    using (SqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read() && !_stop)
                        {
                            bool local;
                            bool temp;
                            try
                            {
                                local = (bool)dbReader["local"];
                            }
                            catch (InvalidCastException)
                            {
                                local = false;
                            }

                            try
                            {
                                temp = (bool)dbReader["temporary"];
                            }
                            catch (InvalidCastException)
                            {
                                temp = false;
                            }

                            string uuid = ((Guid)dbReader["id"]).ToString();
                            sbyte assetType = (sbyte)(byte)dbReader["assetType"];
                            int createTime = (int)dbReader["create_time"];
                            string name = (string)dbReader["name"];
                            string desc = (string)dbReader["description"];
                            byte[] data = (byte[])dbReader["data"];

                            Asset whipAsset =
                                new Asset(
                                    uuid,
                                    (byte)assetType,
                                    local,
                                    temp,
                                    createTime,
                                    name,
                                    desc,
                                    data);

                            try
                            {
                                _server.PutAsset(whipAsset);
                            }
                            catch (Exception e)
                            {
                                Log("Error putting asset: " + e);
                                Console.Read();
                            }

                            Log("Writing " + ((Guid)dbReader["id"]).ToString());
                            Log(++i + " this run");

                            _startAt = (Guid)dbReader["id"];
                        }
                        dbReader.Close();
                        cmd.Dispose();
                    }
                }
                catch (Exception e)
                {
                    Log(e.ToString());
                    Log("Last asset written: " + _startAt);
                }

                Log("Last asset written: " + _startAt);
            }
        }

        public void Log(string message)
        {
            const string PATHNAME = "messagelog.txt";
            Console.WriteLine(message);

            using (StreamWriter sw = new StreamWriter(PATHNAME, true))
            {
                sw.WriteLine(message);
                sw.Close();
            }
        }
    }
}
