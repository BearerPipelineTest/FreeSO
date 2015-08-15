﻿using FSO.Server.Database.DA;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FSO.Server.Database.DA.DbChanges;
using FSO.Server.Common;

namespace FSO.Server.Database.Management
{
    public class DbChangeTool
    {
        private ISqlContext Context;

        public DbChangeTool(ISqlContext context)
        {
            this.Context = context;
        }

        public List<DbChangeScript> GetChanges()
        {
            List<DbChangeScript> changes = new List<DbChangeScript>();
            var connection = Context.Connection;
            var hasChangesTable = connection.Query("SHOW TABLES LIKE 'fso_db_changes'").FirstOrDefault() != null;

            var scriptsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Scripts");
            var manifest = Newtonsoft.Json.JsonConvert.DeserializeObject<DbChangeManifest>(File.ReadAllText(Path.Combine(scriptsDirectory, "manifest.json")));

            foreach(var script in manifest.Changes)
            {
                var scriptData = File.ReadAllText(Path.Combine(scriptsDirectory, script.Script));

                changes.Add(new DbChangeScript {
                    ScriptFilename = script.Script,
                    Idempotent = script.Idempotent,
                    ScriptID = script.ID,
                    ScriptData = scriptData,
                    Status = DbChangeScriptStatus.NOT_INSTALLED,
                    Hash = hashScriptFile(scriptData)
                });
            }

            if (hasChangesTable){
                var changeHistory = connection.Query<DbChange>("SELECT * FROM fso_db_changes");
                foreach(var changeHistoryItem in changeHistory)
                {
                    var match = changes.FirstOrDefault(x => x.ScriptID == changeHistoryItem.id);
                    if(match == null)
                    {
                        continue;
                    }

                    if(match.Hash != changeHistoryItem.hash)
                    {
                        match.Status = DbChangeScriptStatus.MODIFIED;
                    }
                    else
                    {
                        match.Status = DbChangeScriptStatus.INSTALLED;
                    }
                }
            }

            return changes;
        }

        public void ApplyChange(DbChangeScript change)
        {
            var connection = Context.Connection;
            using (var transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead))
            {
                var cmd = transaction.Connection.CreateCommand();
                cmd.CommandText = change.ScriptData;
                cmd.ExecuteNonQuery();

                connection.Execute("INSERT INTO fso_db_changes VALUES (@id, @filename, @date, @hash) ON DUPLICATE KEY UPDATE hash=@hash, date = @date, filename = @filename", new DbChange {
                    id = change.ScriptID,
                    date = Epoch.Now,
                    filename = change.ScriptFilename,
                    hash = change.Hash
                });

                transaction.Commit();
            }
        }

        private string hashScriptFile(string sqlFileData)
        {
            //People often mess up whitespace in sql files in their IDE. Normalize whitespace before hashing
            //to try and avoid changes
            sqlFileData = Regex.Replace(sqlFileData, @"\s+", " ").Trim();

            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(sqlFileData);
            byte[] hash = md5.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return sb.ToString();
        }
    }

    public class DbChangeScript
    {
        public DbChangeScriptStatus Status;
        public string ScriptID;
        public string ScriptFilename;
        public string ScriptData;
        public string Hash;
        public bool Idempotent;
    }

    public enum DbChangeScriptStatus
    {
        INSTALLED,
        MODIFIED,
        NOT_INSTALLED,
        FORCE_REINSTALL
    }





    public class DbChangeManifest
    {
        public List<DbChangeManifestScript> Changes;
    }

    public class DbChangeManifestScript
    {
        public string ID;
        public string Script;
        public bool Idempotent;
    }
}
