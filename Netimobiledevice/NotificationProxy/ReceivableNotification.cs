namespace Netimobiledevice.NotificationProxy
{
    /// <summary>
    /// Device-To-Host (Receivable) notifications.
    /// </summary>
    public enum ReceivableNotification
    {
        SyncCancelRequest,
        SyncSuspendRequst,
        SyncResumeRequst,
        PhoneNumberChanged,
        DeviceNameChanged,
        TimezoneChanged,
        TrustedHostAttached,
        HostDetached,
        HostAttached,
        RegistrationFailed,
        ActivationState,
        BrickState,
        DiskUsageChanged,
        DsDomainChanged,
        AppInstalled,
        AppUninstalled,
        DeveloperImageMounted,
        AttemptActivation,
        ItdbprepDidEnd,
        LanguageChanged,
        AddressBookPreferenceChanged,
        RequestPair
    }
}
