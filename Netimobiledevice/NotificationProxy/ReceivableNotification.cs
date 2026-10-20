using System.Collections.Generic;

namespace Netimobiledevice.NotificationProxy;

/// <summary>
/// Device-To-Host (Receivable) notifications.
/// </summary>
public static class ReceivableNotification {
    /// <summary>
    /// All known receivable notifications.
    /// </summary>
    public static IReadOnlyList<string> All { get; } = [
        ActivationState,
        AddressBookPreferenceChanged,
        AppInstalled,
        AppUninstalled,
        AttemptActivation,
        BackupDomainChanged,
        BrickState,
        DeveloperImageMounted,
        DeviceNameChanged,
        DiskUsageChanged,
        DsDomainChanged,
        HostAttached,
        HostDetached,
        ItdbprepDidEnd,
        LanguageChanged,
        LocalAuthenticationUiDismissed,
        LocalAuthenticationUiPresented,
        PhoneNumberChanged,
        RegistrationFailed,
        RequestPair,
        SyncCancelRequest,
        SyncResumeRequst,
        SyncSuspendRequst,
        TimezoneChanged,
        TrustedHostAttached
    ];

    public static string ActivationState => "com.apple.mobile.lockdown.activation_state";
    public static string AddressBookPreferenceChanged => "com.apple.AddressBook.PreferenceChanged";
    public static string AppInstalled => "com.apple.mobile.application_installed";
    public static string AppUninstalled => "com.apple.mobile.application_uninstalled";
    public static string AttemptActivation => "com.apple.springboard.attemptactivation";
    public static string BackupDomainChanged => "com.apple.mobile.backup.domain_changed";
    public static string BrickState => "com.apple.mobile.lockdown.brick_state";
    public static string DeveloperImageMounted => "com.apple.mobile.developer_image_mounted";
    public static string DeviceNameChanged => "com.apple.mobile.lockdown.device_name_changed";
    public static string DiskUsageChanged => "com.apple.mobile.lockdown.disk_usage_changed";
    public static string DsDomainChanged => "com.apple.mobile.data_sync.domain_changed";
    public static string HostAttached => "com.apple.mobile.lockdown.host_attached";
    public static string HostDetached => "com.apple.mobile.lockdown.host_detached";
    public static string ItdbprepDidEnd => "com.apple.itdbprep.notification.didEnd";
    public static string LanguageChanged => "com.apple.language.changed";
    public const string LocalAuthenticationUiDismissed = "com.apple.LocalAuthentication.ui.dismissed";
    public const string LocalAuthenticationUiPresented = "com.apple.LocalAuthentication.ui.presented";
    public static string PhoneNumberChanged => "com.apple.mobile.lockdown.phone_number_changed";
    public static string RegistrationFailed => "com.apple.mobile.lockdown.registration_failed";
    public static string RequestPair => "com.apple.mobile.lockdown.request_pair";
    public const string SyncCancelRequest = "com.apple.itunes-client.syncCancelRequest";
    public static string SyncResumeRequst => "com.apple.itunes-client.syncResumeRequest";
    public static string SyncSuspendRequst => "com.apple.itunes-client.syncSuspendRequest";
    public static string TimezoneChanged => "com.apple.mobile.lockdown.timezone_changed";
    public static string TrustedHostAttached => "com.apple.mobile.lockdown.trusted_host_attached";
}
