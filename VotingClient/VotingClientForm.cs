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
using System.Numerics;
using CryptoLib;
using Microsoft.FSharp.Core;

namespace VotingClient
{
    public partial class VotingClientForm : Form
    {
        private VotingClient vc;
        private int vote;
        public VotingClientForm()
        {
            InitializeComponent();
            var actLog = new Action<String>(log);
            vc = new VotingClient(actLog);
            fillCmbElections();
        } 
        private void fillCmbElections()
        {
            var eDict = vc.retrieveElections();
            cmbElection.DisplayMember = "Value";
            cmbElection.ValueMember = "Key";
            cmbElection.DataSource = new BindingSource(eDict, null);
        }

        private void fillElectionOptions()
        {
            var eDict = vc.retrieveElectionOptions();
            cmbElectionOptions.DisplayMember = "Key";
            cmbElectionOptions.ValueMember = "Value";
            cmbElectionOptions.DataSource = new BindingSource(eDict, null);

        }

        void log(string msg)
        {
            txtLog.AppendText(msg + "\n");
        }   

        private void btnCastVote_Click(object sender, EventArgs e)
        {
            var c = vc.doEncrypt(vote);
            vc.doInsertVote(c);
        } 
        
        private void cmbElection_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            if (cmbElection.SelectedItem != null)
            {
                vc.ElectionID = cmbElection.SelectedValue.ToString();
                fillElectionOptions();
            }
        }

        private void cmbElectionOptions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbElectionOptions.SelectedItem != null)
            {
                vote = (int)(cmbElectionOptions.SelectedValue);
            }
        }

         

       
    }

    class VotingClient
    {
        private string electionID;

        public string ElectionID
        {
            get { return electionID; }
            set { electionID = value; }
        }

        private SQLiteConnection dbConn;        
        private Action<string> log;

        public VotingClient(Action<string> log)
        {
            initDB();
            this.log = log;
            this.electionID = string.Empty;
        }

        private void initDB()
        {
            var connString = @"Data Source=C:\Users\Panagiotis\Dropbox\Universities\ΜΠΛΑ\Διπλωματική\Code\Millimix\Millimix\bb.db;Version=3;";
            dbConn = new SQLiteConnection(connString);
            dbConn.Open();
           
        }

        public Dictionary<string, string> retrieveElections()
        {

            string sql = "select * from elections";
            SQLiteCommand command = new SQLiteCommand(sql, dbConn);
            SQLiteDataReader reader = command.ExecuteReader();
            var eDict = new Dictionary<string, string>();

            while (reader.Read())
            {
                eDict.Add(reader["id"].ToString(), reader["description"].ToString());
            }
            log("Successfully Retrieved Elections!");

            return eDict;
           
           
        }

        public Dictionary<string, int> retrieveElectionOptions()
        {
            string sql = "Select label,value from ElectionOptions where ElectionID = @Eid ";
            SQLiteCommand command = new SQLiteCommand(sql, dbConn);
            command.Parameters.AddWithValue("Eid", electionID);
            SQLiteDataReader reader = command.ExecuteReader();
            var eDict = new Dictionary<string, int>();

            while (reader.Read())
            {
                eDict.Add(reader["label"].ToString(), Int32.Parse(reader["value"].ToString()));
            }
            log("Successfully Retrieved Election Options For Election " + ElectionID);

            return eDict;

        }
        public Tuple<Tuple<BigInteger, BigInteger, BigInteger>, BigInteger> retrieveKey()
        {
            string sql = "select * from PublicKeys where type = 0 and ElectionID = @Eid";
            SQLiteCommand command = new SQLiteCommand(sql, dbConn);
            command.Parameters.AddWithValue("@Eid", electionID);
            SQLiteDataReader reader = command.ExecuteReader();
            reader.Read();
            var g = BigInteger.Parse(reader["g"].ToString());
            var p = BigInteger.Parse(reader["p"].ToString());
            var q = BigInteger.Parse(reader["q"].ToString());
            var y = BigInteger.Parse(reader["pk"].ToString());
            log("Successfully Retrieved Public Key!" + g + "," + p + "," + q + "," + y);

            var pk = Tuple.Create(Tuple.Create(g, p, q), y);
            return pk;

        }

        public Tuple<BigInteger, BigInteger> doEncrypt(int vote)
        {
            var eg = new ElGamal.ElGamal();
            var pk = retrieveKey();
            var c = eg.Encrypt(vote, pk, FSharpOption<BigInteger>.None);
            log("Cipher" + c.ToString());
            return c;
        }

        public void doInsertVote(Tuple<BigInteger, BigInteger> c)
        {
            SQLiteCommand insertSQL = new SQLiteCommand(
                    "INSERT INTO Votes (Id, CipherG, CipherM, ElectionID) VALUES (@id,@G,@M,@Eid)", dbConn);
            insertSQL.Parameters.AddWithValue("@id", System.Guid.NewGuid());
            insertSQL.Parameters.AddWithValue("@G", c.Item1.ToString());
            insertSQL.Parameters.AddWithValue("@M", c.Item2.ToString());
            insertSQL.Parameters.AddWithValue("@Eid", electionID);
            try
            {
                insertSQL.ExecuteNonQuery();
                log("Vote Cast" + c.ToString());
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        } 

    }
}
