﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TeslaSQL.DataUtils;
using System.Data;

namespace TeslaSQL.Agents {
    /// <summary>
    /// Cleans up old data on the master
    /// </summary>
    public class MasterMaintenance : Agent {
        //base keyword invokes the base class's constructor
        public MasterMaintenance(IDataUtils dataUtils, IDataUtils destDataUtils, Logger logger)
            : base(dataUtils, destDataUtils, logger) {
        }
        public MasterMaintenance() { }

        public override void ValidateConfig() {
            Config.ValidateRequiredHost(Config.Master);
            Config.ValidateRequiredHost(Config.RelayServer);
            if (Config.MasterType == SqlFlavor.None) {
                throw new Exception("MasterMaintenance agent requires a valid SQL flavor for master");
            }
            if (Config.MasterCTDB == null) {
                throw new Exception("MasterMaintenance agent requires masterCTDB to be set");
            }
            if (Config.RelayType == SqlFlavor.None) {
                throw new Exception("MasterMaintenance agent requires a valid SQL flavor for relay");
            }
            if (Config.RelayDB == null) {
                throw new Exception("MasterMaintenance agent requires relayDB to be set");
            }
            if (Config.ChangeRetentionHours <= 0) {
                throw new Exception("MasterMaintenance agent requires changeRetentionHours to be set and positive");
            }
        }

        public override void Run() {
            var chopDate = DateTime.Now - new TimeSpan(Config.ChangeRetentionHours, 0, 0);
            var CTIDs = destDataUtils.GetOldCTIDsMaster(Config.RelayDB, chopDate);
            var tables = sourceDataUtils.GetTables(Config.MasterCTDB);
            if (tables.Count() > 0) {
                logger.Log("Deleting {" + string.Join(",", CTIDs) + "} from { " + string.Join(",", tables.Select(t => t.name)) + "}", LogLevel.Debug);
                MaintenanceHelper.DeleteOldTables(CTIDs, tables, sourceDataUtils, Config.MasterCTDB);
            } else {
                logger.Log("No tables to delete", LogLevel.Info);
            }

            if (Config.MasterType == SqlFlavor.MySQL)
            {
                foreach (var table in Config.Tables)
                {
                    var temp = (MySQLDataUtils)sourceDataUtils;
                    temp.CleanupTriggerTable(Config.MasterDB, table.Name, chopDate);
                }
            }
        }
    }
}
