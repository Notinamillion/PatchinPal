using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using PatchinPal.Common;

namespace PatchinPal.Server
{
    /// <summary>
    /// Manages persistence of machine information to JSON files
    /// </summary>
    public class MachineRepository
    {
        private readonly string _dataPath;
        private readonly string _machinesFile;
        private readonly JavaScriptSerializer _serializer;
        private Dictionary<string, MachineInfo> _machines;

        public MachineRepository()
        {
            _dataPath = ConfigurationManager.AppSettings["DataPath"] ?? @"C:\ProgramData\PatchinPal\Server";
            _machinesFile = Path.Combine(_dataPath, "machines.json");
            _serializer = new JavaScriptSerializer();
            _machines = new Dictionary<string, MachineInfo>();

            // Ensure directory exists
            if (!Directory.Exists(_dataPath))
            {
                Directory.CreateDirectory(_dataPath);
                Console.WriteLine($"Created data directory: {_dataPath}");
            }
        }

        /// <summary>
        /// Load machines from JSON file
        /// </summary>
        public void Load()
        {
            try
            {
                if (!File.Exists(_machinesFile))
                {
                    Console.WriteLine("No existing machine database found. Starting fresh.");
                    return;
                }

                string json = File.ReadAllText(_machinesFile);
                var machineList = _serializer.Deserialize<List<MachineInfo>>(json);

                _machines.Clear();
                foreach (var machine in machineList)
                {
                    _machines[machine.IpAddress] = machine;
                }

                Console.WriteLine($"Loaded machine database from: {_machinesFile}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error loading machine database: {ex.Message}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Save machines to JSON file
        /// </summary>
        public void Save()
        {
            try
            {
                var machineList = _machines.Values.ToList();
                string json = _serializer.Serialize(machineList);

                // Pretty print JSON
                json = PrettyPrintJson(json);

                File.WriteAllText(_machinesFile, json);
                Console.WriteLine($"Saved {machineList.Count} machine(s) to database");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error saving machine database: {ex.Message}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Add or update a machine in the repository
        /// </summary>
        public void AddOrUpdateMachine(MachineInfo machine)
        {
            if (_machines.ContainsKey(machine.IpAddress))
            {
                // Update existing machine
                var existing = _machines[machine.IpAddress];

                // Preserve some fields from existing record
                machine.LastUpdateCheck = existing.LastUpdateCheck;

                // Update fields
                existing.HostName = machine.HostName;
                existing.OsVersion = machine.OsVersion;
                existing.OsBuild = machine.OsBuild;
                existing.IsOnline = machine.IsOnline;
                existing.LastSeen = machine.LastSeen;
                existing.Status = machine.Status;
            }
            else
            {
                // Add new machine
                _machines[machine.IpAddress] = machine;
            }
        }

        /// <summary>
        /// Get a machine by IP address
        /// </summary>
        public MachineInfo GetMachine(string ipAddress)
        {
            return _machines.ContainsKey(ipAddress) ? _machines[ipAddress] : null;
        }

        /// <summary>
        /// Get all machines
        /// </summary>
        public List<MachineInfo> GetAllMachines()
        {
            return _machines.Values.ToList();
        }

        /// <summary>
        /// Remove a machine from the repository
        /// </summary>
        public bool RemoveMachine(string ipAddress)
        {
            return _machines.Remove(ipAddress);
        }

        /// <summary>
        /// Get machines that haven't been seen in a specified time period
        /// </summary>
        public List<MachineInfo> GetStaleMachines(TimeSpan threshold)
        {
            var cutoff = DateTime.Now - threshold;
            return _machines.Values.Where(m => m.LastSeen < cutoff).ToList();
        }

        /// <summary>
        /// Get statistics about the machines
        /// </summary>
        public (int total, int online, int offline, int needingUpdates) GetStatistics()
        {
            var machines = _machines.Values.ToList();
            return (
                total: machines.Count,
                online: machines.Count(m => m.IsOnline),
                offline: machines.Count(m => !m.IsOnline),
                needingUpdates: machines.Count(m => m.PendingUpdates > 0)
            );
        }

        /// <summary>
        /// Simple JSON pretty printer
        /// </summary>
        private string PrettyPrintJson(string json)
        {
            try
            {
                // Simple indentation
                int indent = 0;
                var result = new System.Text.StringBuilder();
                bool inQuotes = false;

                for (int i = 0; i < json.Length; i++)
                {
                    char ch = json[i];

                    if (ch == '"')
                    {
                        inQuotes = !inQuotes;
                        result.Append(ch);
                    }
                    else if (!inQuotes)
                    {
                        if (ch == '{' || ch == '[')
                        {
                            result.Append(ch);
                            result.Append(Environment.NewLine);
                            indent++;
                            result.Append(new string(' ', indent * 2));
                        }
                        else if (ch == '}' || ch == ']')
                        {
                            result.Append(Environment.NewLine);
                            indent--;
                            result.Append(new string(' ', indent * 2));
                            result.Append(ch);
                        }
                        else if (ch == ',')
                        {
                            result.Append(ch);
                            result.Append(Environment.NewLine);
                            result.Append(new string(' ', indent * 2));
                        }
                        else if (ch == ':')
                        {
                            result.Append(ch);
                            result.Append(' ');
                        }
                        else if (!char.IsWhiteSpace(ch))
                        {
                            result.Append(ch);
                        }
                    }
                    else
                    {
                        result.Append(ch);
                    }
                }

                return result.ToString();
            }
            catch
            {
                return json;
            }
        }
    }
}
