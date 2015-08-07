using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySql.Data.MySqlClient;
using System.Data;
using InWorldz.Whip.Client;

namespace whipstress
{
    class AssetImport
    {
        MySqlConnection _conn;
        RemoteServer _server;
        int _startAt;
        bool _stop = false;

        public AssetImport(RemoteServer server, string connstring, int startAt)
        {
            _conn = new MySqlConnection(connstring);
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
            int count = 0;

            MySqlCommand countCmd =
                    new MySqlCommand(
                        "SELECT COUNT(*) as CNT " +
                        "FROM assets " +
                        "WHERE create_time >= ?createTime ",
                        _conn);

            countCmd.Parameters.AddWithValue("?createTime", _startAt);

            countCmd.CommandType = CommandType.Text;
            count = Convert.ToInt32(countCmd.ExecuteScalar());
            countCmd.Dispose();

            MySqlCommand cmd =
                    new MySqlCommand(
                        "SELECT id, name, description, assetType, local, temporary, data, create_time " +
                        "FROM assets " +
                        "WHERE create_time >= ?createTime " +
                        "ORDER BY create_time",
                        _conn);

            cmd.Parameters.AddWithValue("?createTime", _startAt);



            try
            {
                using (MySqlDataReader dbReader = cmd.ExecuteReader())
                {
                    while (dbReader.Read() && ! _stop)
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

                        string uuid = (string)dbReader["id"];
                        sbyte assetType = (sbyte) dbReader["assetType"];
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
                            Console.WriteLine("Error putting asset: " + e.Message);
                        }

                        Console.WriteLine("Writing " + (string)dbReader["id"]);
                        Console.WriteLine(++i + " of " + count);

                        _startAt = (int)dbReader["create_time"];
                    }
                    dbReader.Close();
                    cmd.Dispose();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("Last time written: " + _startAt);
            }

            Console.WriteLine("Last time written: " + _startAt);
        }
    }
}
