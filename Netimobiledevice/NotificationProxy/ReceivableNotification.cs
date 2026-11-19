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
        SysDiagnoseStopped,
        TimezoneChanged,
        TrustedHostAttached
    ];

    public const string ActivationState = "com.apple.mobile.lockdown.activation_state";
    public const string AddressBookPreferenceChanged = "com.apple.AddressBook.PreferenceChanged";
    public const string AppInstalled = "com.apple.mobile.application_installed";
    public const string AppUninstalled = "com.apple.mobile.application_uninstalled";
    public const string AttemptActivation = "com.apple.springboard.attemptactivation";
    public const string BackupDomainChanged = "com.apple.mobile.backup.domain_changed";
    public const string BrickState = "com.apple.mobile.lockdown.brick_state";
    public const string DeveloperImageMounted = "com.apple.mobile.developer_image_mounted";
    public const string DeviceNameChanged = "com.apple.mobile.lockdown.device_name_changed";
    public const string DiskUsageChanged = "com.apple.mobile.lockdown.disk_usage_changed";
    public const string DsDomainChanged = "com.apple.mobile.data_sync.domain_changed";
    public const string HostAttached = "com.apple.mobile.lockdown.host_attached";
    public const string HostDetached = "com.apple.mobile.lockdown.host_detached";
    public const string ItdbprepDidEnd = "com.apple.itdbprep.notification.didEnd";
    public const string LanguageChanged = "com.apple.language.changed";
    public const string LocalAuthenticationUiDismissed = "com.apple.LocalAuthentication.ui.dismissed";
    public const string LocalAuthenticationUiPresented = "com.apple.LocalAuthentication.ui.presented";
    public const string PhoneNumberChanged = "com.apple.mobile.lockdown.phone_number_changed";
    public const string RegistrationFailed = "com.apple.mobile.lockdown.registration_failed";
    public const string RequestPair = "com.apple.mobile.lockdown.request_pair";
    public const string SyncCancelRequest = "com.apple.itunes-client.syncCancelRequest";
    public const string SyncResumeRequst = "com.apple.itunes-client.syncResumeRequest";
    public const string SyncSuspendRequst = "com.apple.itunes-client.syncSuspendRequest";
    public const string SysDiagnoseStopped = "com.apple.sysdiagnose.sysdiagnoseStopped";
    public const string TimezoneChanged = "com.apple.mobile.lockdown.timezone_changed";
    public const string TrustedHostAttached = "com.apple.mobile.lockdown.trusted_host_attached";
}
