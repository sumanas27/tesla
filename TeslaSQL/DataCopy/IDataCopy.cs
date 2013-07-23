﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TeslaSQL.DataCopy {
    public interface IDataCopy {
        /// <summary>
        /// Copy the contents of a table from source to destination
        /// </summary>
        /// <param name="sourceDB">Source database name</param>
        /// <param name="sourceTableName">Table name</param>
        /// <param name="schema">Table's schema</param>
        /// <param name="destDB">Destination database name</param>
        /// <param name="timeout">Used as timeout for both the query and the bulk copy</param>
        void CopyTable(string sourceDB, string sourceTableName, string schema, string destDB, int timeout, string destTableName = null, string originalTableName = null);

        /// <summary>
        /// Copies the table from sourceDB.sourceTableName over to destDB.destTableName. Deletes the existing destination table first if it exists
        /// </summary>
        void CopyTableDefinition(string sourceDB, string sourceTableName, string schema, string destDB, string destTableName, string originalTableName = null);
    }
}
