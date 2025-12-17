using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace PatchinPal.Common
{
    /// <summary>
    /// Represents a machine on the network
    /// </summary>
    [DataContract]
    public class MachineInfo
    {
        [DataMember]
        public string IpAddress { get; set; }

        [DataMember]
        public string HostName { get; set; }

        [DataMember]
        public string OsVersion { get; set; }

        [DataMember]
        public string OsBuild { get; set; }

        [DataMember]
        public bool IsOnline { get; set; }

        [DataMember]
        public DateTime LastSeen { get; set; }

        [DataMember]
        public DateTime? LastUpdateCheck { get; set; }

        [DataMember]
        public int PendingUpdates { get; set; }

        [DataMember]
        public int ClientPort { get; set; } = 8090;

        [DataMember]
        public UpdateStatus Status { get; set; }
    }

    /// <summary>
    /// Represents the status of updates on a machine
    /// </summary>
    public enum UpdateStatus
    {
        Unknown,
        UpToDate,
        UpdatesAvailable,
        Updating,
        Failed,
        RebootRequired
    }

    /// <summary>
    /// Command sent from server to client
    /// </summary>
    [DataContract]
    public class UpdateCommand
    {
        [DataMember]
        public CommandType Type { get; set; }

        [DataMember]
        public bool AggressiveMode { get; set; }

        [DataMember]
        public DateTime? ScheduledTime { get; set; }
    }

    /// <summary>
    /// Types of commands the server can send
    /// </summary>
    public enum CommandType
    {
        CheckForUpdates,
        InstallUpdates,
        ScheduleUpdate,
        GetStatus,
        Reboot,
        CancelScheduledUpdate
    }

    /// <summary>
    /// Response from client to server
    /// </summary>
    [DataContract]
    public class UpdateResponse
    {
        [DataMember]
        public bool Success { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public List<WindowsUpdate> AvailableUpdates { get; set; }

        [DataMember]
        public UpdateStatus Status { get; set; }

        [DataMember]
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Represents a Windows update
    /// </summary>
    [DataContract]
    public class WindowsUpdate
    {
        [DataMember]
        public string Title { get; set; }

        [DataMember]
        public string Description { get; set; }

        [DataMember]
        public string KbArticleId { get; set; }

        [DataMember]
        public long SizeInBytes { get; set; }

        [DataMember]
        public bool IsDownloaded { get; set; }

        [DataMember]
        public bool IsInstalled { get; set; }

        [DataMember]
        public bool RebootRequired { get; set; }

        [DataMember]
        public UpdateSeverity Severity { get; set; }
    }

    /// <summary>
    /// Severity of Windows updates
    /// </summary>
    public enum UpdateSeverity
    {
        Low,
        Moderate,
        Important,
        Critical
    }

    /// <summary>
    /// Status report sent from client to server
    /// </summary>
    [DataContract]
    public class ClientStatusReport
    {
        [DataMember]
        public string IpAddress { get; set; }

        [DataMember]
        public string HostName { get; set; }

        [DataMember]
        public string OsVersion { get; set; }

        [DataMember]
        public string OsBuild { get; set; }

        [DataMember]
        public bool IsOnline { get; set; }

        [DataMember]
        public DateTime LastSeen { get; set; }

        [DataMember]
        public DateTime? LastUpdateCheck { get; set; }

        [DataMember]
        public int PendingUpdates { get; set; }

        [DataMember]
        public int ClientPort { get; set; }

        [DataMember]
        public UpdateStatus Status { get; set; }

        [DataMember]
        public List<WindowsUpdate> AvailableUpdates { get; set; }
    }
}
