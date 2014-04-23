using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.Data.SQLite;
using CryptoLib;

namespace ElectionCreator
{
    public partial class frmElection : Form
    {
        public frmElection()
        {
            InitializeComponent();
        }

        void log(string msg)
        {
            txtLog.AppendText(msg + "\n");
        }
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                initDB();
                var eid = insertElection();
                log("Created Election" + eid);
                insertElectionOptions(eid);
                log("Created Election Options For " + eid);
                insertKey(eid);
                log("Success");
            }
            catch (Exception ex)
            {
                log(ex.ToString());
            }
           
        }

        SQLiteConnection dbConn;
        private void initDB()
        {
            var connString = @"Data Source=C:\Users\Panagiotis\Dropbox\Universities\ΜΠΛΑ\Διπλωματική\Code\MixPerm\MixPerm\bb.db;Version=3;";
            dbConn = new SQLiteConnection(connString);
            dbConn.Open();
        }

        private Guid insertElection()
        {
            var desc = txtElectionName.Text.Trim();
            var cmd = dbConn.CreateCommand();
            var eID = System.Guid.NewGuid();
            cmd.CommandText = " INSERT INTO Elections (ID, Description) " +
                              " VALUES (@ID, @Desc) ";
            cmd.Parameters.AddWithValue("@ID", eID);
            cmd.Parameters.AddWithValue("@Desc", desc);
       
            cmd.ExecuteNonQuery();

            return eID;
        }

        private void insertElectionOptions(Guid eid)
        {
            var cmd = dbConn.CreateCommand();
            var options = txtElectionOptions.Text.Trim();
            var c = Environment.NewLine + "\t";
            var ls = options.Split(c.ToCharArray());
            foreach (var l in ls)
            {
                if (l==string.Empty) continue;
                var opt = l.Split( new char[]{' '} );
                cmd.CommandText = " INSERT INTO ElectionOptions (ID, Label, Value, ElectionID) " +
                              " VALUES (@ID, @Label, @Value, @ElectionID) ";
                cmd.Parameters.AddWithValue("@ID", System.Guid.NewGuid());
                cmd.Parameters.AddWithValue("@Label", opt[0]);
                cmd.Parameters.AddWithValue("@Value", Int32.Parse(opt[1]));
                cmd.Parameters.AddWithValue("@ElectionID", eid.ToString());
                cmd.ExecuteNonQuery();
            }
        }

        private void insertKey(Guid eid)
        {
            var n = Int32.Parse(txtLength.Text.Trim());
            var eg = new ElGamal.ElGamal();
            var keys = eg.KeyGen(n);
            log("Secret Key="+keys.Item2.Item2.ToString());

            var cmd = dbConn.CreateCommand();
            cmd.CommandText = " INSERT INTO PublicKeys (ID, g, p, q, pk, Type, ElectionID) " +
                              " VALUES (@ID, @g, @p, @q, @pk, @Type, @ElectionID) ";
            cmd.Parameters.AddWithValue("@ID", System.Guid.NewGuid());
            cmd.Parameters.AddWithValue("@g", keys.Item1.Item1.Item1);
            cmd.Parameters.AddWithValue("@p", keys.Item1.Item1.Item2);
            cmd.Parameters.AddWithValue("@q", keys.Item1.Item1.Item3);
            cmd.Parameters.AddWithValue("@pk", keys.Item1.Item2);
            cmd.Parameters.AddWithValue("@Type", 0);
            cmd.Parameters.AddWithValue("@ElectionID", eid.ToString());
            cmd.ExecuteNonQuery();

        }
    }
}
