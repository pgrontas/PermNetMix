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
using System.Numerics;
using Microsoft.FSharp.Core;
using Microsoft.FSharp.Collections;

//type synonyms
 
using PEPProof = System.Tuple<System.Numerics.BigInteger, System.Numerics.BigInteger, System.Numerics.BigInteger, 
                               System.Tuple<System.Tuple<System.Numerics.BigInteger, System.Numerics.BigInteger, System.Numerics.BigInteger>, System.Numerics.BigInteger>>;
using DISPEPPRoof = System.Tuple<System.Collections.Generic.IEnumerable<System.Numerics.BigInteger>, System.Collections.Generic.IEnumerable<System.Numerics.BigInteger>, System.Collections.Generic.IEnumerable<System.Numerics.BigInteger>,
                                    System.Collections.Generic.IEnumerable<System.Tuple<System.Tuple<System.Numerics.BigInteger, System.Numerics.BigInteger, System.Numerics.BigInteger>, System.Numerics.BigInteger>>>;

namespace MixServer
{
    public partial class Form1 : Form
    {
        VotingClient vc;
        public Form1()
        {
            InitializeComponent();
            vc = new VotingClient(this.log);
            fillCmbElections();
        }

        private void fillCmbElections()
        {
            var eDict = vc.retrieveElections();
            cmbElection.DisplayMember = "Value";
            cmbElection.ValueMember = "Key";
            cmbElection.DataSource = new BindingSource(eDict, null);
        }   

        void log(string msg)
        {
            txtLog.AppendText(msg+"\n");
        }

        private void cmbElection_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbElection.SelectedItem != null) {
                var election = cmbElection.SelectedValue.ToString();
                vc.ElectionID = election;
            }
        }

        private void btnMix_Click(object sender, EventArgs e)
        {
            vc.doMixing();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            vc.Decrypt();
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
        private int stage;

        public VotingClient(Action<string> log)
        {
            initDB();
            this.log = log;
            this.electionID = string.Empty;
        }

        private void initDB()
        {
            var connString = @"Data Source=C:\Users\Panagiotis\Dropbox\Universities\ΜΠΛΑ\Διπλωματική\Code\MixPerm\MixPerm\bb.db;Version=3;";
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

        public Tuple<Tuple<BigInteger, BigInteger, BigInteger>, BigInteger> retrieveKey()
        {
            string sql = "select g,p,q,pk from PublicKeys where type = 0 and ElectionID = @Eid";
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


        internal void doMixing()
        { 
            var pk = retrieveKey();
            stage = getMixStage();
            if (stage > 0)
            {
                var proofs = retrieveProofs(stage);
                var ok = PermNet.verify(proofs);
                if (!ok)
                {
                    log("Validation of mixing stage " + stage + " has failed!!!");
                    log("Mixing cannot proceed");
                    return;
                }
            }
                
            var votes = retrieveVotes();
            if (votes.Count > 0) {
                var mixResults = PermNet.mix(votes, pk);
                votes = mixResults.Item1.ToList();
                var newProofs= mixResults.Item2.ToList();
                stage++;
                using (var transaction = dbConn.BeginTransaction()){
                    try
                    {
                        reInsertVotes(votes);
                        reInsertProofs(newProofs);
                        transaction.Commit();
                    }
                    catch (Exception e)
                    {
                        log("Error ReInserting Votes" + e.ToString());
                        return;
                    }                    
                }
            }
        }

        private void reInsertProofs(List<Tuple<PEPProof,DISPEPPRoof>> newProofs)
        {
            
            using (var cmd = new SQLiteCommand(dbConn))
            {
               
                  
                    foreach (var proof in newProofs)
                    {
                        var pep = proof.Item1;
                        var dpep = proof.Item2;
                        var pepGuid = System.Guid.NewGuid();
                        var dpepGuid = System.Guid.NewGuid();

                        cmd.CommandText = "INSERT INTO MixProofs (ID, PEPID, DISPEPID, Stage, ElectionID) " +
                                           "VALUES (@ID, @PEPID, @DISPEPID,@Stage, @Eid )";
                        cmd.Parameters.AddWithValue("@ID", System.Guid.NewGuid());
                        cmd.Parameters.AddWithValue("@PEPID", pepGuid);
                        cmd.Parameters.AddWithValue("@DISPEPID", dpepGuid);
                        cmd.Parameters.AddWithValue("@Stage", stage);
                        cmd.Parameters.AddWithValue("@Eid", electionID);
                        cmd.ExecuteNonQuery();

                        insertPEP(cmd, pep, pepGuid);

                        var offers = dpep.Item1.ToArray();
                        var challenges = dpep.Item2.ToArray();
                        var responses = dpep.Item3.ToArray();
                        var pks = dpep.Item4.ToArray();
                        for (int i = 0; i < offers.Length; i++)
                        {
                            PEPProof pr = Tuple.Create(offers[i], challenges[i], responses[i], pks[i]);
                            insertPEP(cmd, pr, dpepGuid);
                        }

                    }

              
                    log("Mixed Votes Stage" + stage);
                
            }
        }

        private static void insertPEP(SQLiteCommand cmd, PEPProof pep, Guid pepGuid)
        {
            cmd.CommandText = "INSERT INTO SchnorrProof (ID, offer, challenge, response, g, p, q,y, ProofID) " +
                               "VALUES (@ID, @offer, @challenge, @response, @g, @p, @q, @y, @ProofID)";
            cmd.Parameters.AddWithValue("@ID", System.Guid.NewGuid());
            cmd.Parameters.AddWithValue("@offer", pep.Item1);
            cmd.Parameters.AddWithValue("@challenge", pep.Item2);
            cmd.Parameters.AddWithValue("@response", pep.Item3);
            cmd.Parameters.AddWithValue("@g", pep.Item4.Item1.Item1);
            cmd.Parameters.AddWithValue("@p", pep.Item4.Item1.Item2);
            cmd.Parameters.AddWithValue("@q", pep.Item4.Item1.Item3);
            cmd.Parameters.AddWithValue("@y", pep.Item4.Item2);
            cmd.Parameters.AddWithValue("@ProofID", pepGuid);
            cmd.ExecuteNonQuery();
        }
     
        private List<Tuple<PEPProof, DISPEPPRoof>> retrieveProofs(int stage)
        {
            
            var pls = retrievePEProofs(stage);
            var dpls = retrieveDPEProofs(stage);
            var ls = new List<Tuple<PEPProof, DISPEPPRoof>>();
            for (int i = 0; i < pls.Count; i++)
                ls.Add(Tuple.Create(pls[i], dpls[i]));
            return ls;
        }

        private List<PEPProof> retrievePEProofs(int stage)
        {
            log("Retrieving PEP Proofs For Mix Stage " + (stage).ToString());
            var sql = " select offer,challenge,response,g,p,q,y as pk " +
                      " from SchnorrProof sp inner join mixproofs mp on sp.proofID = mp.PEPID " +
                      " where electionID = @Eid and Stage = @stage";
            var command = new SQLiteCommand(sql, dbConn);
            command.Parameters.AddWithValue("@Eid", electionID);
            command.Parameters.AddWithValue("@Stage", stage);
            var reader = command.ExecuteReader();
            var ls = new List<PEPProof>();
            while (reader.Read())
            {
                var g = BigInteger.Parse(reader["g"].ToString());
                var p = BigInteger.Parse(reader["p"].ToString());
                var q = BigInteger.Parse(reader["q"].ToString());
                var y = BigInteger.Parse(reader["pk"].ToString());
                var pk = Tuple.Create(Tuple.Create(g, p, q), y);
                var offer = BigInteger.Parse(reader["offer"].ToString());
                var challenge = BigInteger.Parse(reader["challenge"].ToString());
                var response = BigInteger.Parse(reader["response"].ToString());
                var t = Tuple.Create(offer, challenge, response, pk);
                ls.Add(t);
            }
            log("Retrieved " + ls.Count + " PEP Proofs");
            return ls;
        }

        private List<DISPEPPRoof> retrieveDPEProofs(int stage)
        {
            log("Retrieving DPE Proofs For Mix Stage " + (stage).ToString());
            var sql = " select offer,challenge,response,g,p,q,y as pk, proofID " +
                      " from SchnorrProof sp inner join mixproofs mp on sp.proofID = mp.DISPEPID" +
                      " where electionID = @Eid and Stage = @stage";
            var command = new SQLiteCommand(sql, dbConn);
            command.Parameters.AddWithValue("@Eid", electionID);
            command.Parameters.AddWithValue("@Stage", stage);
            var reader = command.ExecuteReader();

            var ls1 = new List<Tuple<PEPProof,string>>();
            while (reader.Read())
            {
                var g = BigInteger.Parse(reader["g"].ToString());
                var p = BigInteger.Parse(reader["p"].ToString());
                var q = BigInteger.Parse(reader["q"].ToString());
                var y = BigInteger.Parse(reader["pk"].ToString());
                var pk = Tuple.Create(Tuple.Create(g, p, q), y);
                var offer = BigInteger.Parse(reader["offer"].ToString());
                var challenge = BigInteger.Parse(reader["challenge"].ToString());
                var response = BigInteger.Parse(reader["response"].ToString());
                var t = Tuple.Create(offer, challenge, response, pk);
                var proofID = reader["proofID"].ToString();
                ls1.Add(Tuple.Create(t,proofID));
            }
            log("Retrieved " + ls1.Count + " DPEP Proofs");

            var ls2 = ls1.GroupBy(x => x.Item2);
            var ls = new List<DISPEPPRoof>();
            foreach (var l in ls2)
            {
                var offers = l.Select(t => t.Item1.Item1);
                var challenges = l.Select(t => t.Item1.Item2);
                var responses = l.Select(t => t.Item1.Item3);
                var pks = l.Select(t => t.Item1.Item4);
                ls.Add(Tuple.Create(offers, challenges, responses, pks));
            }
           
            return ls;
        }
        private void reInsertVotes(List<Tuple<BigInteger,BigInteger>> votes)
        {
            var n = votes.Count;
            using (var cmd = new SQLiteCommand(dbConn))
            {
                 
                    for (var i = 0; i < n; i++)
                    {
                        cmd.CommandText = "INSERT INTO Mix (ID, CipherG, CipherM, Stage, ElectionID) "+
                                           "VALUES (@ID, @CipherG, @CipherM,@Stage, @Eid )";
                        cmd.Parameters.AddWithValue("@ID", System.Guid.NewGuid());
                        cmd.Parameters.AddWithValue("@CipherG", votes[i].Item1.ToString());
                        cmd.Parameters.AddWithValue("@CipherM", votes[i].Item2.ToString());
                        cmd.Parameters.AddWithValue("@Stage", stage);
                        cmd.Parameters.AddWithValue("@Eid", electionID);
                        cmd.ExecuteNonQuery();
                    }

                   
                    log("Mixed Votes Stage" + stage);
                 
            }
        }

        private List<Tuple<BigInteger,BigInteger>> retrieveVotes()
        {
          
            stage = getMixStage();
            var fromMix = stage > 0;
            if (fromMix)
            {                
                log("Mixing Stage " + (stage + 1).ToString());
                var sql = "select CipherG,CipherM from Mix where electionID = @Eid and Stage = @stage";
                var command = new SQLiteCommand(sql, dbConn);
                command.Parameters.AddWithValue("@Eid", electionID);
                command.Parameters.AddWithValue("@Stage", stage);
                var reader = command.ExecuteReader();
                var ls = new List<Tuple<BigInteger, BigInteger>>();
                while (reader.Read())
                {
                    var t = Tuple.Create<BigInteger, BigInteger>(BigInteger.Parse(reader["CipherG"].ToString()), BigInteger.Parse(reader["CipherM"].ToString()));
                    ls.Add(t);
                }
                log("Retrieved " + ls.Count +" Votes");
                return ls;
            }
            else //this is the first mix server
            {
                var n = 0;
                log("Preparing for Mix Stage 1");
                var sql = "select count(*) as total from Votes where electionID = @Eid";
                var command = new SQLiteCommand(sql, dbConn);
                command.Parameters.AddWithValue("@Eid", electionID);
                
                n = Int32.Parse (command.ExecuteScalar().ToString());
                var n1 = Common.closestPowerOf2(n);
                if (n != n1)
                {
                    sql = "select max(value) from electionOptions where electionID = @Eid";
                    command = new SQLiteCommand(sql, dbConn);
                    command.Parameters.AddWithValue("@Eid", electionID);
                    var mx = Int32.Parse (command.ExecuteScalar().ToString());
                    var rnd = new Random();
                    log("Inserting Dummy Votes");
                    for (int i = n + 1; i <= n1; i++)
                    {
                        var r = rnd.Next(mx+1,mx+n1-n);
                        var c = doEncrypt(r);
                        doInsertVote(c);
                    }
                }

                sql = "select CipherG,CipherM from Votes where electionID = @Eid";
                command = new SQLiteCommand(sql, dbConn);
                command.Parameters.AddWithValue("@Eid", electionID);
                var reader = command.ExecuteReader();
                var ls = new List<Tuple<BigInteger, BigInteger>>();
                while (reader.Read())
                {
                    var t = Tuple.Create<BigInteger, BigInteger>(BigInteger.Parse(reader["CipherG"].ToString()),  BigInteger.Parse(reader["CipherM"].ToString()));
                    ls.Add(t);
                }
              
                
                log("Retrieved " + ls.Count + " Votes");
                return ls;
            }
        }

       

        private int getMixStage()
        {
            string sql = "select max(stage) as mstage from mix where electionID = @Eid";
            var command = new SQLiteCommand(sql, dbConn);
            command.Parameters.AddWithValue("@Eid", electionID);
            var res = command.ExecuteScalar();
            bool fromMix = (res != DBNull.Value);
            if (fromMix){
                stage = Int32.Parse(res.ToString());
                return stage;
            }
            else { 
                stage = 0;
                return 0;
            }
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

        internal void Decrypt()
        {
            var options = retrieveElectionOptions();
            var counters = new Dictionary<int,int>();
            foreach (var item in options)
                counters.Add(item.Value, 0);

            var votes = retrieveVotes();
            var pk = retrieveKey();

            string[] lines = System.IO.File.ReadAllLines(@"SK.txt");
            var k = BigInteger.Parse(lines[0]);
            Tuple<Tuple<BigInteger, BigInteger, BigInteger>, BigInteger> sk =
                Tuple.Create(pk.Item1, k);
            
            var eg = new ElGamal.ElGamal();
            var sum = new BigInteger(0);
            foreach (var vote in votes)
            {
                var m = (int)eg.Decrypt(vote, sk);
                log("Decrypted vote " + vote + " to " + m);
                if (counters.ContainsKey(m))
                    counters[m]++;
            }

            foreach (var res in counters)
                log("Option " + res.Key + " Received " + res.Value + " Votes ");
        }
    }

   
}
