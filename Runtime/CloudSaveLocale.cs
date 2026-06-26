using System;

namespace Wagenheimer.CloudSave
{
    public static class CloudSaveLocale
    {
        /// <summary>
        /// Assign this delegate to integrate with any localization system (e.g. I2 Localization).
        /// Called with a string key; should return the translated text.
        /// When null, built-in English fallback strings are used.
        ///
        /// Usage (I2):
        ///   CloudSaveLocale.Translate = key => LocalizationManager.GetTermTranslation(key);
        /// </summary>
        public static Func<string, string> Translate { get; set; }

        static string Get(string key) => Translate?.Invoke(key) ?? Fallback(key);

        static string Fallback(string key) => key switch
        {
            "cloudsave.loading"             => "Syncing save",
            "cloudsave.synced"              => "Cloud save applied",
            "cloudsave.local_newer"         => "Local save is up to date",
            "cloudsave.local_kept"          => "Local save kept",
            "cloudsave.offline"             => "No connection \u2014 local save",
            "cloudsave.error"               => "Failed to sync save",
            "cloudsave.account_linked"      => "Account linked: {0}",
            "cloudsave.account_switched"    => "Account recovered \u2014 syncing...",
            "cloudsave.conflict_title_cloud"   => "Cloud save is newer",
            "cloudsave.conflict_title_account" => "Save from another account found",
            "cloudsave.conflict_choose"     => "Choose which save to use:",
            "cloudsave.conflict_local"      => "Local Save",
            "cloudsave.conflict_cloud"      => "Cloud Save",
            "cloudsave.conflict_none"       => "No save",
            "cloudsave.btn_keep_local"      => "Keep Local",
            "cloudsave.btn_use_cloud"       => "Use Cloud",
            "cloudsave.sync_status_synced"  => "Synced",
            "cloudsave.sync_status_syncing" => "Syncing...",
            "cloudsave.sync_status_offline" => "Offline",
            "cloudsave.sync_status_error"   => "Sync error",
            "cloudsave.sync_last"           => "Last sync: {0}",
            "cloudsave.auth_title"          => "Cloud Login",
            "cloudsave.auth_description"    => "Link your account to access your save on other devices.",
            "cloudsave.auth_status_anonymous" => "Account: Anonymous",
            "cloudsave.auth_status_linked"  => "Account: {0}",
            "cloudsave.auth_btn_google"     => "Sign in with Google Play Games",
            "cloudsave.auth_btn_apple"      => "Sign in with Apple Game Center",
            "cloudsave.auth_btn_signin_apple" => "Sign in with Apple",
            "cloudsave.auth_btn_close"      => "Not now",
            _ => key
        };

        // ── Convenience accessors ───────────────────────────────────────────

        public static string Loading()            => Get("cloudsave.loading");
        public static string Synced()              => Get("cloudsave.synced");
        public static string LocalNewer()           => Get("cloudsave.local_newer");
        public static string LocalKept()            => Get("cloudsave.local_kept");
        public static string Offline()              => Get("cloudsave.offline");
        public static string Error()                => Get("cloudsave.error");
        public static string AccountLinked(string p) => string.Format(Get("cloudsave.account_linked"), p);
        public static string AccountSwitched()      => Get("cloudsave.account_switched");
        public static string ConflictTitleCloud()   => Get("cloudsave.conflict_title_cloud");
        public static string ConflictTitleAccount() => Get("cloudsave.conflict_title_account");
        public static string ConflictChoose()       => Get("cloudsave.conflict_choose");
        public static string ConflictLocal()         => Get("cloudsave.conflict_local");
        public static string ConflictCloud()         => Get("cloudsave.conflict_cloud");
        public static string ConflictNone()          => Get("cloudsave.conflict_none");
        public static string BtnKeepLocal()          => Get("cloudsave.btn_keep_local");
        public static string BtnUseCloud()           => Get("cloudsave.btn_use_cloud");
        public static string SyncStatus(SyncStatus s) => s switch
        {
            SyncStatus.Synced  => Get("cloudsave.sync_status_synced"),
            SyncStatus.Syncing => Get("cloudsave.sync_status_syncing"),
            SyncStatus.Offline => Get("cloudsave.sync_status_offline"),
            SyncStatus.Error   => Get("cloudsave.sync_status_error"),
            _ => ""
        };
        public static string SyncLast(string t)     => string.Format(Get("cloudsave.sync_last"), t);
        public static string AuthTitle()             => Get("cloudsave.auth_title");
        public static string AuthDescription()       => Get("cloudsave.auth_description");
        public static string AuthStatusAnonymous()   => Get("cloudsave.auth_status_anonymous");
        public static string AuthStatusLinked(string p) => string.Format(Get("cloudsave.auth_status_linked"), p);
        public static string AuthBtnGoogle()          => Get("cloudsave.auth_btn_google");
        public static string AuthBtnApple()           => Get("cloudsave.auth_btn_apple");
        public static string AuthBtnSignInApple()     => Get("cloudsave.auth_btn_signin_apple");
        public static string AuthBtnClose()           => Get("cloudsave.auth_btn_close");
    }

    public enum SyncStatus
    {
        Synced,
        Syncing,
        Offline,
        Error
    }
}
