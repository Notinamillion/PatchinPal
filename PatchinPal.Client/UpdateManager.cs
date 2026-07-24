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
        private readonly object _lock = new object();

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
            ISearchResult searchResult = null;
            try
            {
                Console.WriteLine("Searching for updates...");

                // Search for updates that are not installed
                searchResult = _updateSearcher.Search("IsInstalled=0 and Type='Software'");

                var results = new List<WindowsUpdate>();

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

                    results.Add(windowsUpdate);
                }

                lock (_lock)
                {
                    _cachedUpdates = results;
                    _lastUpdateCheck = DateTime.Now;
                }

                Console.WriteLine($"Found {_cachedUpdates.Count} update(s)");
                return new List<WindowsUpdate>(_cachedUpdates);
            }
            catch (COMException ex)
            {
                Console.WriteLine($"Error checking for updates: {ex.Message}");
                return new List<WindowsUpdate>();
            }
            finally
            {
                ReleaseComObject(searchResult);
            }
        }

        /// <summary>
        /// Install available updates
        /// </summary>
        /// <param name="aggressive">If true, installs all updates without prompting and forces installation</param>
        public UpdateResponse InstallUpdates(bool aggressive)
        {
            ISearchResult searchResult = null;
            IDownloadResult downloadResult = null;
            IInstallationResult installResult = null;
            UpdateCollection updatesToInstall = null;
            IUpdateDownloader downloader = null;
            IUpdateInstaller installer = null;

            try
            {
                // First check for updates if cache is empty or old
                List<WindowsUpdate> cached;
                DateTime lastCheck;
                lock (_lock)
                {
                    cached = _cachedUpdates;
                    lastCheck = _lastUpdateCheck;
                }

                if (cached.Count == 0 || (DateTime.Now - lastCheck).TotalMinutes > 30)
                {
                    CheckForUpdates();
                    lock (_lock)
                    {
                        cached = _cachedUpdates;
                    }
                }

                if (cached.Count == 0)
                {
                    return new UpdateResponse
                    {
                        Success = true,
                        Message = "No updates available",
                        Status = UpdateStatus.UpToDate,
                        Timestamp = DateTime.Now
                    };
                }

                Console.WriteLine($"Installing {cached.Count} update(s)...");

                // Search for updates to get IUpdate objects for installation
                searchResult = _updateSearcher.Search("IsInstalled=0 and Type='Software'");

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
                updatesToInstall = new UpdateCollection();

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
                downloader = _updateSession.CreateUpdateDownloader();
                downloader.Updates = updatesToInstall;

                downloadResult = downloader.Download();

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
                installer = _updateSession.CreateUpdateInstaller();
                installer.Updates = updatesToInstall;

                if (aggressive)
                {
                    installer.AllowSourcePrompts = false;
                }

                installResult = installer.Install();

                bool rebootRequired = installResult.RebootRequired;
                bool success = installResult.ResultCode == OperationResultCode.orcSucceeded
                            || installResult.ResultCode == OperationResultCode.orcSucceededWithErrors;

                // Clear cache after installation
                lock (_lock)
                {
                    _cachedUpdates.Clear();
                }

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
            finally
            {
                // Clean up COM objects to prevent memory leaks
                ReleaseComObject(installResult);
                ReleaseComObject(installer);
                ReleaseComObject(downloadResult);
                ReleaseComObject(downloader);
                if (updatesToInstall != null)
                {
                    for (int i = 0; i < updatesToInstall.Count; i++)
                    {
                        ReleaseComObject(updatesToInstall[i]);
                    }
                    ReleaseComObject(updatesToInstall);
                }
                ReleaseComObject(searchResult);
            }
        }

        /// <summary>
        /// Get current update status
        /// </summary>
        public UpdateResponse GetStatus()
        {
            List<WindowsUpdate> updates;
            lock (_lock)
            {
                updates = new List<WindowsUpdate>(_cachedUpdates);
            }

            return new UpdateResponse
            {
                Success = true,
                Message = "Status retrieved",
                AvailableUpdates = updates,
                Status = updates.Count > 0 ? UpdateStatus.UpdatesAvailable : UpdateStatus.UpToDate,
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

        /// <summary>
        /// Safely release a COM object
        /// </summary>
        private static void ReleaseComObject(object obj)
        {
            if (obj != null)
            {
                try
                {
                    Marshal.ReleaseComObject(obj);
                }
                catch
                {
                    // Ignore release failures
                }
            }
        }
    }
}
