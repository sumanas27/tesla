﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TeslaSQL.DataUtils;
using System.Data.SqlClient;
using Microsoft.SqlServer.Management.Smo;
using System.Data;
using System.Data.OleDb;
using System.Diagnostics;

namespace TeslaSQL.DataCopy {
    public class MSSQLToNetezzaDataCopy : IDataCopy {

        private MSSQLDataUtils sourceDataUtils;
        private NetezzaDataUtils destDataUtils;
        private Logger logger;

        public MSSQLToNetezzaDataCopy(MSSQLDataUtils sourceDataUtils, NetezzaDataUtils destDataUtils, Logger logger) {
            this.sourceDataUtils = sourceDataUtils;
            this.destDataUtils = destDataUtils;
            this.logger = logger;
        }

        public void CopyTable(string sourceDB, string sourceTableName, string schema, string destDB, int timeout, string destTableName = null) {
            //by default the dest table will have the same name as the source table
            destTableName = (destTableName == null) ? sourceTableName : destTableName;

            //drop table at destination and create from source schema
            CopyTableDefinition(sourceDB, sourceTableName, schema, destDB, destTableName);

            var cols = GetColumns(sourceDB, sourceTableName, schema);
            var bcpSelect = string.Format("SELECT {0} FROM {1}..{2};",
                                          string.Join(",", cols.Select(col => col.name)),
                                          sourceDB, sourceTableName);
            if (bcpSelect.Length > 3800) {
                //BCP commands fail if their text length is over 4000 characters, and we need some padding
                //drop view CTVWtablename if exists
                //create view CTVWtablename AS $bcpSelect
                string viewName = "CTVW" + sourceTableName;
                sourceDataUtils.RecreateView(sourceDB, viewName, bcpSelect);
                bcpSelect = string.Format("SELECT * FROM {0}..{1}", sourceDB, viewName);
            }
            var bcpArgs = string.Format(@"""{0}"" queryout \\bonas1a\sql_temp\{1}\{2}.txt -T -c -S{3} -t""|"" -r\n",
                                            bcpSelect,
                                            sourceDB,
                                            destTableName,
                                            "owl\\feeds"
                                            );
            logger.Log(bcpArgs, LogLevel.Trace);
            var p = new Process();
            p.StartInfo.FileName = "bcp";
            p.StartInfo.Arguments = bcpArgs;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardError = true;
            p.Start();
            p.WaitForExit();
            if (p.ExitCode != 0) {
                logger.Log(p.StandardError.ReadToEnd(), LogLevel.Critical);
                throw new Exception("BCP error");
            }
        }

        private void CopyDataFromQuery(string sourceDB, string destDB, SqlCommand cmd, string destTableName, string schema, int timeout, int timeout_2) {
            throw new NotImplementedException();
        }

        struct Col {
            public string name;
            public string datatype;
            public Col(string name, string datatype) {
                this.name = name;
                this.datatype = datatype;
            }
            public override string ToString() {
                return name + " " + datatype;
            }
        }

        public void CopyTableDefinition(string sourceDB, string sourceTableName, string schema, string destDB, string destTableName) {
            var cols = GetColumns(sourceDB, sourceTableName, schema);
            string nzCreate = string.Format(
                @"CREATE TABLE {0}
                            (
                                {1} NOT NULL
                            ) DISTRIBUTE ON RANDOM;",
                destTableName,
                string.Join(",", cols));
            logger.Log(nzCreate, LogLevel.Trace);
            destDataUtils.DropTableIfExists(destDB, destTableName, schema);
            var cmd = new OleDbCommand(nzCreate);
            destDataUtils.SqlNonQuery(destDB, cmd);

        }

        private List<Col> GetColumns(string sourceDB, string sourceTableName, string schema) {
            var table = sourceDataUtils.GetSmoTable(sourceDB, sourceTableName, schema);

            var shortenedTypes = new HashSet<SqlDataType> {
                SqlDataType.Binary,
                SqlDataType.VarBinary,
                SqlDataType.VarBinaryMax,
                SqlDataType.Char,
                SqlDataType.NChar,
                SqlDataType.NVarChar,
                SqlDataType.NVarCharMax,
                SqlDataType.VarChar,
                SqlDataType.VarCharMax,
            };
            var shortenedNumericTypes = new HashSet<SqlDataType>{
                SqlDataType.Decimal,
                SqlDataType.Numeric
            };
            var cols = new List<Col>();
            foreach (Column col in table.Columns) {

                //--a few hard coded exceptions - ignoring for now
                //if @tablename = 'tbljoinMarketTestSegmentVariation'
                //    SELECT @columnlist = REPLACE(@columnlist, 'NVARCHAR(500)', 'NVARCHAR(100)')

                //if @tablename = 'SM_tracking_log'
                //    SELECT @columnlist = REPLACE(@columnlist, 'CTID', 'CT_ID')

                string dataType = col.DataType.Name;

                string modDataType = DataType.MapDataType(SqlFlavor.MSSQL, SqlFlavor.Netezza, dataType);
                if (dataType != modDataType) {
                    cols.Add(new Col(col.Name, modDataType));
                    continue;
                }
                if (shortenedTypes.Contains(col.DataType.SqlDataType)) {
                    dataType += "(" + ((col.DataType.MaximumLength > 500 || col.DataType.MaximumLength < 1) ? 500 : col.DataType.MaximumLength) + ")";
                } else if (shortenedNumericTypes.Contains(col.DataType.SqlDataType)) {
                    dataType += "(" + col.DataType.NumericPrecision + ")";
                }
                cols.Add(new Col(col.Name, dataType));
            }
            return cols;
        }
    }
}
