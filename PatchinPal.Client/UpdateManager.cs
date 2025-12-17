using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using PatchinPal.Common;
using Interop.WUApiLib;

namespace PatchinPal.Client
{
    /// <summary>
    /// Manages Windows Update operations using the Windows Update Agent API
    /// </summary>
    public class UpdateManager
    {
        private UpdateSession _updateSession;
        private IUpdateSearcher _updateSearcher;
        private List<WindowsUpdate> _cachedUpdates;
        private DateTime _lastUpdateCheck;

        public UpdateManager()
        {
            try
            {
                _updateSession = new UpdateSession();
                _updateSearcher = _updateSession.CreateUpdateSearcher();
                _cachedUpdates = new List<WindowsUpdate>();
                _lastUpdateCheck = DateTime.MinValue;
            }
            catch (COMException ex)
            {
                Console.WriteLine($"Failed to initialize Windows Update API: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Check for available Windows updates
        /// </summary>
        public List<WindowsUpdate> CheckForUpdates()
        {
            try
            {
                Console.WriteLine("Searching for updates...");

                // Search for updates that are not installed
                ISearchResult searchResult = _updateSearcher.Search("IsInstalled=0 and Type='Software'");

                _cachedUpdates.Clear();

                foreach (IUpdate update in searchResult.Updates)
                {
                    var windowsUpdate = new WindowsUpdate
                    {
                        Title = update.Title,
                        Description = update.Description,
                        KbArticleId = update.KBArticleIDs.Count > 0 ? update.KBArticleIDs[0] : "",
                        SizeInBytes = (long)update.MaxDownloadSize,
                        IsDownloaded = update.IsDownloaded,
                        IsInstalled = update.IsInstalled,
                        RebootRequired = false, // Will be determined after installation
                        Severity = MapSeverity(update)
                    };

                    _cachedUpdates.Add(windowsUpdate);
                }

                _lastUpdateCheck = DateTime.Now;
                Console.WriteLine($"Found {_cachedUpdates.Count} update(s)");

                return _cachedUpdates;
            }
            catch (COMException ex)
            {
                Console.WriteLine($"Error checking for updates: {ex.Message}");
                return new List<WindowsUpdate>();
            }
        }

        /// <summary>
        /// Install available updates
        /// </summary>
        /// <param name="aggressive">If true, installs all updates without prompting and forces installation</param>
        public UpdateResponse InstallUpdates(bool aggressive)
        {
            try
            {
                // First check for updates if cache is empty or old
                if (_cachedUpdates.Count == 0 || (DateTime.Now - _lastUpdateCheck).TotalMinutes > 30)
                {
                    CheckForUpdates();
                }

                if (_cachedUpdates.Count == 0)
                {
                    return new UpdateResponse
                    {
                        Success = true,
                        Message = "No updates available",
                        Status = UpdateStatus.UpToDate,
                        Timestamp = DateTime.Now
                    };
                }

                Console.WriteLine($"Installing {_cachedUpdates.Count} update(s)...");

                // Search for updates again to get IUpdate objects
                ISearchResult searchResult = _updateSearcher.Search("IsInstalled=0 and Type='Software'");

                if (searchResult.Updates.Count == 0)
                {
                    return new UpdateResponse
                    {
                        Success = true,
                        Message = "No updates to install",
                        Status = UpdateStatus.UpToDate,
                        Timestamp = DateTime.Now
                    };
                }

                // Create update collection
                UpdateCollection updatesToInstall = new UpdateCollection();

                foreach (IUpdate update in searchResult.Updates)
                {
                    if (update.EulaAccepted == false)
                    {
                        update.AcceptEula();
                    }
                    updatesToInstall.Add(update);
                }

                // Download updates if needed
                Console.WriteLine("Downloading updates...");
                var downloader = _updateSession.CreateUpdateDownloader();
                downloader.Updates = updatesToInstall;

                IDownloadResult downloadResult = downloader.Download();

                if (downloadResult.ResultCode != OperationResultCode.orcSucceeded
                    && downloadResult.ResultCode != OperationResultCode.orcSucceededWithErrors)
                {
                    return new UpdateResponse
                    {
                        Success = false,
                        Message = $"Download failed with code: {downloadResult.ResultCode}",
                        Status = UpdateStatus.Failed,
                        Timestamp = DateTime.Now
                    };
                }

                // Install updates
                Console.WriteLine("Installing updates...");
                var installer = _updateSession.CreateUpdateInstaller();
                installer.Updates = updatesToInstall;

                if (aggressive)
                {
                    installer.AllowSourcePrompts = false;
                    // ForceQuiet not available in this API version, but AllowSourcePrompts=false provides similar behavior
                }

                IInstallationResult installResult = installer.Install();

                bool rebootRequired = installResult.RebootRequired;
                bool success = installResult.ResultCode == OperationResultCode.orcSucceeded
                            || installResult.ResultCode == OperationResultCode.orcSucceededWithErrors;

                // Clear cache after installation
                _cachedUpdates.Clear();

                return new UpdateResponse
                {
                    Success = success,
                    Message = $"Installation completed with result: {installResult.ResultCode}",
                    Status = rebootRequired ? UpdateStatus.RebootRequired : UpdateStatus.UpToDate,
                    Timestamp = DateTime.Now
                };
            }
            catch (COMException ex)
            {
                return new UpdateResponse
                {
                    Success = false,
                    Message = $"Installation failed: {ex.Message}",
                    Status = UpdateStatus.Failed,
                    Timestamp = DateTime.Now
                };
            }
        }

        /// <summary>
        /// Get current update status
        /// </summary>
        public UpdateResponse GetStatus()
        {
            return new UpdateResponse
            {
                Success = true,
                Message = "Status retrieved",
                AvailableUpdates = _cachedUpdates,
                Status = _cachedUpdates.Count > 0 ? UpdateStatus.UpdatesAvailable : UpdateStatus.UpToDate,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// Map Windows Update severity to our enum
        /// </summary>
        private UpdateSeverity MapSeverity(IUpdate update)
        {
            try
            {
                // Check if update is in important or recommended categories
                foreach (ICategory category in update.Categories)
                {
                    string catName = category.Name.ToLower();

                    if (catName.Contains("critical") || catName.Contains("security"))
                        return UpdateSeverity.Critical;
                    if (catName.Contains("important"))
                        return UpdateSeverity.Important;
                }

                // Check MsrcSeverity if available
                if (!string.IsNullOrEmpty(update.MsrcSeverity))
                {
                    switch (update.MsrcSeverity.ToLower())
                    {
                        case "critical": return UpdateSeverity.Critical;
                        case "important": return UpdateSeverity.Important;
                        case "moderate": return UpdateSeverity.Moderate;
                        case "low": return UpdateSeverity.Low;
                    }
                }

                return UpdateSeverity.Moderate;
            }
            catch
            {
                return UpdateSeverity.Moderate;
            }
        }
    }
}
