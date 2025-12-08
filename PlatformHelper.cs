using System;
using System.IO;

namespace NoPasaranFC
{
    /// <summary>
    /// Platform-specific path helper for cross-platform compatibility
    /// </summary>
    public static class PlatformHelper
    {
        private static string _dataPath;
        private static string _contentPath;
        private static bool _sqliteInitialized;

#if ANDROID
        // Store Android context for file access - use global:: to avoid namespace conflicts
        private static global::Android.Content.Context _androidContext;
        
        /// <summary>
        /// Initialize with Android context. Call this from Activity.OnCreate before any database operations.
        /// </summary>
        public static void SetAndroidContext(global::Android.Content.Context context)
        {
            _androidContext = context;
        }
#endif

        /// <summary>
        /// Initialize SQLite for the current platform. Must be called before any database operations.
        /// </summary>
        public static void InitializeSQLite()
        {
            if (_sqliteInitialized) return;
            
#if ANDROID
            // On Android, SQLitePCL needs to be initialized with the bundled native library
            SQLitePCL.Batteries_V2.Init();
#endif
            _sqliteInitialized = true;
        }

        /// <summary>
        /// Gets the writable data path for saving database and user files
        /// </summary>
        public static string DataPath
        {
            get
            {
                if (_dataPath == null)
                {
#if ANDROID
                    // On Android, use the app's internal files directory from context
                    if (_androidContext != null)
                    {
                        _dataPath = _androidContext.FilesDir.AbsolutePath;
                    }
                    else
                    {
                        // Fallback to Personal folder
                        _dataPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                        if (string.IsNullOrEmpty(_dataPath))
                        {
                            _dataPath = "/data/data/com.nopasaranfc.game/files";
                        }
                    }
#else
                    // On desktop, use the application base directory
                    _dataPath = AppDomain.CurrentDomain.BaseDirectory;
#endif
                }
                return _dataPath;
            }
        }

        /// <summary>
        /// Gets the content/assets path for reading game resources
        /// </summary>
        public static string ContentPath
        {
            get
            {
                if (_contentPath == null)
                {
#if ANDROID
                    // On Android, content is accessed through the Content manager, not file paths
                    _contentPath = string.Empty;
#else
                    _contentPath = AppDomain.CurrentDomain.BaseDirectory;
#endif
                }
                return _contentPath;
            }
        }

        /// <summary>
        /// Gets the full path for the database file
        /// </summary>
        public static string GetDatabasePath()
        {
            return Path.Combine(DataPath, "nopasaran.db");
        }

        /// <summary>
        /// Gets the full path for an asset file (for desktop only)
        /// On Android, assets should be accessed through the Content manager
        /// </summary>
        public static string GetAssetPath(string relativePath)
        {
#if ANDROID
            // On Android, return relative path for asset manager access
            return relativePath;
#else
            return Path.Combine(ContentPath, relativePath);
#endif
        }

        /// <summary>
        /// Checks if running on Android platform
        /// </summary>
        public static bool IsAndroid
        {
            get
            {
#if ANDROID
                return true;
#else
                return false;
#endif
            }
        }
    }
}
