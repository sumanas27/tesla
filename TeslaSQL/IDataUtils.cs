using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Data;

namespace TeslaSQL {
    public interface IDataUtils {
        /// <summary>
        /// Gets information on the last CT batch relevant to this agent
        /// </summary>
        /// <param name="server">Server identifier</param>
        /// <param name="dbName">Database name</param>
        /// <param name="agentType">We need to query a different table for master vs. slave</param>
        /// <param name="slaveIdentifier">Hostname of the slave if applicable</param>
        DataRow GetLastCTBatch(TServer server, string dbName, AgentType agentType, string slaveIdentifier = "");

        /// <summary>
        /// Gets CT versions that are greater than the passed in CTID and have the passed in bitwise value
        /// </summary>
        /// <param name="server">Server to check</param>
        /// <param name="dbName">Database name to check</param>
        /// <param name="CTID">Pull CTIDs greater than this one</param>
        /// <param name="syncBitWise">Only include versions containing this bit</param>
        DataTable GetPendingCTVersions(TServer server, string dbName, Int64 CTID, int syncBitWise);

        /// <summary>
        /// Gets the start time of the last successful CT batch before the specified CTID
        /// </summary>
        /// <param name="server">Server identifier</param>
        /// <param name="dbName">Database name</param>
        /// <param name="CTID">Current CTID</param>
        /// <param name="syncBitWise">syncBitWise value to compare against</param>
        /// <returns>Datetime representing last succesful run</returns>
        DateTime GetLastStartTime(TServer server, string dbName, Int64 CTID, int syncBitWise);

        /// <summary>
        /// Gets the CHANGE_TRACKING_CURRENT_VERSION() for a database
        /// </summary>
        /// <param name="server">Server identifer</param>
        /// <param name="dbName">Database name</param>
        /// <returns>Current change tracking version</returns>
        Int64 GetCurrentCTVersion(TServer server, string dbName);

        /// <summary>
        /// Gets the minimum valid CT version for a table
        /// </summary>
        /// <param name="server">Server identifier</param>
        /// <param name="dbName">Database name</param>
        /// <param name="table">Table name</param>
        /// <param name="schema">Table's schame</param>
        /// <returns>Minimum valid version</returns>
        Int64 GetMinValidVersion(TServer server, string dbName, string table, string schema);

        /// <summary>
        /// Creates a new row in tblCTVersion
        /// </summary>
        /// <param name="server">Server identifier</param>
        /// <param name="dbName">Database name</param>
        /// <param name="syncStartVersion">Version number the batch starts at</param>
        /// <param name="syncStopVersion">Version number the batch ends at</param>
        /// <returns>CTID generated by the database</returns>
        Int64 CreateCTVersion(TServer server, string dbName, Int64 syncStartVersion, Int64 syncStopVersion);

        /// <summary>
        /// Generates and runs SELECT INTO query to create a changetable
        /// </summary>
        /// <param name="server">Server identifer</param>
        /// <param name="sourceCTDB">Source CT database name</param>
        /// <param name="schemaName">Source schema name</param>
        /// <param name="masterColumnList">column list for the select statement</param>
        /// <param name="ctTableName">CT table name</param>
        /// <param name="sourceDB">Source database name</param>
        /// <param name="tableName">Table name</param>
        /// <param name="startVersion">syncStartVersion for the batch</param>
        /// <param name="pkList">Primary key list for join condition</param>
        /// <param name="stopVersion">syncStopVersion for the batch</param>
        /// <param name="notNullPkList">Primary key list for where clause</param>
        /// <param name="timeout">How long this is allowed to run for (seconds)</param>
        /// <returns>Int representing the number of rows affected (number of changes captured)</returns>
        int SelectIntoCTTable(TServer server, string sourceCTDB, string masterColumnList, string ctTableName,
            string sourceDB, string schemaName, string tableName, Int64 startVersion, string pkList, Int64 stopVersion, string notNullPkList, int timeout);

        /// <summary>
        /// Creates a row in tblCTSlaveVersion
        /// </summary>
        /// <param name="server">Server identifier to write to</param>
        /// <param name="dbName">Database name to write to</param>
        /// <param name="slaveIdentifier">Slave identifier string (usually hostname)</param>
        /// <param name="CTID">Batch number (generated on master)</param>
        /// <param name="syncStartVersion">Version number the batch starts at</param>
        /// <param name="syncStopVersion">Version number the batch ends at</param>
        /// <param name="syncBitWise">Current bitwise value for the batch</param>
        /// <param name="syncStartTime">Time the batch started on the master</param>
        void CreateSlaveCTVersion(TServer server, string dbName, Int64 CTID, string slaveIdentifier,
            Int64 syncStartVersion, Int64 syncStopVersion, DateTime syncStartTime, Int32 syncBitWise);

        /// <summary>
        /// Create the tblCTSchemaChange_(version) table on the relay server, dropping if it already exists
        /// </summary>
        /// <param name="dbName">Database to run on</param>
        /// <param name="CTID">CT version number</param>
        void CreateSchemaChangeTable(TServer server, string dbName, Int64 CTID);

        /// <summary>
        /// Get DDL events from tblDDLEvent that occurred after the specified date
        /// </summary>
        /// <param name="server">Server identifier</param>
        /// <param name="dbName">Database name</param>
        /// <param name="afterDate">Date to start from</param>
        /// <returns>DataTable object representing the events</returns>
        DataTable GetDDLEvents(TServer server, string dbName, DateTime afterDate);

        /// <summary>
        /// Writes a schema change record to the appropriate schema change table
        /// </summary>
        /// <param name="server">Server identifier</param>
        /// <param name="dbName">Database name</param>
        /// <param name="CTID">Batch ID</param>
        /// <param name="ddeID">DDL event identifier from source database</param>
        /// <param name="eventType">Type of schema change ( i.e. add/drop/modify/rename)</param>
        /// <param name="schemaName">Schema name this applies to (usually dbo)</param>
        /// <param name="tableName">Table name this schema change applies to</param>
        /// <param name="columnName">Column name</param>
        /// <param name="previousColumnName">Previous column name (applicable only to renames)</param>
        /// <param name="baseType">Basic data type of the column (applicable to add and modify)</param>
        /// <param name="characterMaximumLength">Maximum length for string columns (i.e. varchar, nvarchar)</param>
        /// <param name="numericPrecision">Numeric precision (for decimal/numeric columns)</param>
        /// <param name="numericScale">Numeric scale (for decimal/numeric columns)</param>
        void WriteSchemaChange(TServer server, string dbName, Int64 CTID, int ddeID, string eventType, string schemaName, string tableName,
            string columnName, string newColumnName, string baseType, int? characterMaximumLength, int? numericPrecision, int? numericScale);

        /// <summary>
        /// Gets a column's data type
        /// </summary>
        /// <param name="server">Server identifier</param>
        /// <param name="dbName">Database name</param>
        /// <param name="table">Table name</param>
        /// <param name="schema">The table's schema</param>
        /// <param name="column">Column name to get the data type of</param>
        /// <returns>DataRow representing the data type</returns>
        DataRow GetDataType(TServer server, string dbName, string table, string schema, string column);

        /// <summary>
        /// Updates the syncStopVersion in tblCTVersion to the specified value for the specified CTID
        /// </summary>
        /// <param name="server">Server identifier</param>
        /// <param name="dbName">Database name</param>
        /// <param name="syncStopVersion">New syncStopVersion</param>
        /// <param name="CTID">Batch identifier</param>
        void UpdateSyncStopVersion(TServer server, string dbName, Int64 syncStopVersion, Int64 CTID);

        /// <summary>
        /// Check to see if a table exists on the specified server
        /// </summary>
        /// <param name="server">Server to check</param>
        /// <param name="dbName">Database name</param>
        /// <param name="table">Table name to check for</param>
        /// <param name="schema">Table's schema</param>
        /// <returns>Boolean representing whether or not the table exists.</returns>
        bool CheckTableExists(TServer server, string dbName, string table, string schema = "dbo");

        /// <summary>
        /// Compares two tables and retrieves a column list that is an intersection of the columns they contain
        /// </summary>
        /// <param name="server">Server identifier</param>
        /// <param name="dbName">Database name</param>
        /// <param name="table1">First table</param>
        /// <param name="schema1">First table's schema</param>
        /// <param name="table2">Second table (order doesn't matter)</param>
        /// <param name="schema2">Second table's schema</param>
        /// <returns>String containing the resulting intersect column list</returns>
        string GetIntersectColumnList(TServer server, string dbName, string table1, string schema1, string table2, string schema2);

        /// <summary>
        /// Check whether a table has a primary key
        /// </summary>
        /// <param name="server">Server identifier</param>
        /// <param name="dbName">Database name</param>
        /// <param name="table">First table</param>
        /// <param name="schema">Schema name</param>
        /// <returns>True if the table has a primary key, otherwise false</returns>
        bool HasPrimaryKey(TServer server, string dbName, string table, string schema);

        /// <summary>
        /// Checks to see if a table exists on the specified server and drops it if so.
        /// </summary>
        /// <param name="server">Server identifier</param>
        /// <param name="dbName">Database name</param>
        /// <param name="table">Table name</param>
        /// <param name="schema">Table's schema</param>
        /// <returns>Boolean specifying whether or not the table existed</returns>
        bool DropTableIfExists(TServer server, string dbName, string table, string schema);

        /// <summary>
        /// Copy the contents of a table from source to destination
        /// </summary>
        /// <param name="sourceServer">Source server identifier</param>
        /// <param name="sourceDB">Source database name</param>
        /// <param name="table">Table name</param>
        /// <param name="schema">Table's schema</param>
        /// <param name="destServer">Destination server identifier</param>
        /// <param name="destDB">Destination database name</param>
        /// <param name="timeout">Used as timeout for both the query and the bulk copy</param>
        void CopyTable(TServer sourceServer, string sourceDB, string table, string schema, TServer destServer, string destDB, int timeout);

        /// <summary>
        /// Gets a dictionary of columns for a table
        /// </summary>
        /// <param name="server">Server identifier</param>
        /// <param name="dbName">Database name</param>
        /// <param name="table">Table name</param>
        /// <param name="schema">Table's schema</param>
        /// <returns>Dictionary with column name as key and a bool representing whether it's part of the primary key as value</returns>
        Dictionary<string, bool> GetFieldList(TServer server, string dbName, string table, string schema);

        /// <summary>
        /// Adds the specified bit to the syncBitWise column in tblCTVersion/tblCTSlaveVersion
        /// </summary>
        /// <param name="server">Server identifier to write to</param>
        /// <param name="dbName">Database name</param>
        /// <param name="CTID">CT version number</param>
        /// <param name="value">Bit to add</param>
        /// <param name="agentType">Agent type running this request (if it's slave we use tblCTSlaveVersion)</param>
        void WriteBitWise(TServer server, string dbName, Int64 CTID, int value, AgentType agentType);

        /// <summary>
        /// Gets syncbitwise for specified CT version table
        /// </summary>
        /// <param name="server">Server identifier to read from</param>
        /// <param name="dbName">Database name</param>
        /// <param name="CTID">CT version number to check</param>
        /// <param name="agentType">Agent type running this request (if it's slave we use tblCTSlaveVersion)</param>
        int ReadBitWise(TServer server, string dbName, Int64 CTID, AgentType agentType);

        /// <summary>
        /// Marks a CT batch as complete
        /// </summary>
        /// <param name="server">Server identifier</param>
        /// <param name="dbName">Database name</param>
        /// <param name="CTID">CT batch ID</param>
        /// <param name="syncBitWise">Final bitwise value to write</param>
        /// <param name="syncStopTime">Stop time to write</param>
        /// <param name="agentType">config.AgentType calling this</param>
        /// <param name="slaveIdentifier">For slave agents, the slave hostname or ip</param>
        void MarkBatchComplete(TServer server, string dbName, Int64 CTID, Int32 syncBitWise, DateTime syncStopTime,
            AgentType agentType, string slaveIdentifier = "");

        /// <summary>
        /// Pulls the list of schema changes for a CTID
        /// </summary>
        /// <param name="server">Server identifier</param>
        /// <param name="dbName">Database name</param>
        /// <param name="CTID">change tracking batch ID</param>
        /// <returns>DataTable object containing the query results</returns>
        DataTable GetSchemaChanges(TServer server, string dbName, Int64 CTID);

        /// <summary>
        /// Gets the rowcounts for a table
        /// </summary>
        /// <param name="server">Server identifier</param>
        /// <param name="dbName">Database name</param>
        /// <param name="table">Table name</param>
        /// <param name="schema">Table's schema</param>
        /// <returns>The number of rows in the table</returns>
        Int64 GetTableRowCount(TServer server, string dbName, string table, string schema);

        /// <summary>
        /// Checks whether change tracking is enabled on a table
        /// </summary>
        /// <param name="server">Server identifier</param>
        /// <param name="dbName">Database name</param>
        /// <param name="table">Table name</param>
        /// <param name="schema">Table's schema</param>
        /// <returns>True if it is enabled, false if it's not.</returns>
        bool IsChangeTrackingEnabled(TServer server, string dbName, string table, string schema);

        /// <summary>
        /// Renames a column in a table, and the associated history table if recording history is configured
        /// <summary>
        /// <param name="t">TableConf object for the table</param>
        /// <param name="server">Server identifer where the table lives</param>
        /// <param name="dbName">Database name the table lives in</param>
        /// <param name="schema">Schema the table is part of</param>
        /// <param name="table">Table name</param>
        /// <param name="columnName">Old column name</param>
        /// <param name="newColumnName">New column name</param>
        void RenameColumn(TableConf t, TServer server, string dbName, string schema, string table, 
            string columnName, string newColumnName);

        /// <summary>
        /// Changes a column's data type
        /// </summary>
        /// <param name="t">TableConf object for the table</param>
        /// <param name="server">Server identifer where the table lives</param>
        /// <param name="dbName">Database name the table lives in</param>
        /// <param name="schema">Schema the table is part of</param>
        /// <param name="table">Table name</param>
        /// <param name="columnName">Column name to modify</param>
        /// <param name="baseType">Base data type (i.e. varchar, int)</param>
        /// <param name="characterMaximumLength">Max length for *char data types</param>
        /// <param name="numericPrecision">Numeric precision for numeric/decimal types</param>
        /// <param name="numericScale">Numeric scale for numeric/decimal types</param>
        void ModifyColumn(TableConf t, TServer server, string dbName, string schema, string table,
            string columnName, string baseType, int? characterMaximumLength, int? numericPrecision, int? numericScale);

        /// <summary>
        /// Adds a column to a table
        /// </summary>
        /// <param name="t">TableConf object for the table</param>
        /// <param name="server">Server identifer where the table lives</param>
        /// <param name="dbName">Database name the table lives in</param>
        /// <param name="schema">Schema the table is part of</param>
        /// <param name="table">Table name</param>
        /// <param name="columnName">Column name to add</param>
        /// <param name="baseType">Base data type (i.e. varchar, int)</param>
        /// <param name="characterMaximumLength">Max length for *char data types</param>
        /// <param name="numericPrecision">Numeric precision for numeric/decimal types</param>
        /// <param name="numericScale">Numeric scale for numeric/decimal types</param>
        void AddColumn(TableConf t, TServer server, string dbName, string schema, string table,
            string columnName, string baseType, int? characterMaximumLength, int? numericPrecision, int? numericScale);

        /// <summary>
        /// Drops a column from a table
        /// </summary>
        /// <param name="t">TableConf object for the table</param>
        /// <param name="server">Server identifer where the table lives</param>
        /// <param name="dbName">Database name the table lives in</param>
        /// <param name="schema">Schema the table is part of</param>
        /// <param name="table">Table name</param>
        /// <param name="columnName">Column name to drop</param>
        void DropColumn(TableConf t, TServer server, string dbName, string schema, string table, string columnName);

        void LogError(string message);

        DataTable GetUnsentErrors();
        void MarkErrorsSent(IEnumerable<int> celIds);
    }
}
