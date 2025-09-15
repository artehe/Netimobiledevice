using System.Diagnostics.CodeAnalysis;

namespace Netimobiledevice.NotificationProxy;

/// <summary>
/// Device-To-Host (Receivable) notifications.
/// </summary>
public static class ReceivableNotification
{
    public static string ActivationState => "com.apple.mobile.lockdown.activation_state";
    public static string AddressBookPreferenceChanged => "com.apple.AddressBook.PreferenceChanged";
    public static string AppInstalled => "com.apple.mobile.application_installed";
    public static string AppUninstalled => "com.apple.mobile.application_uninstalled";
    public static string AttemptActivation => "com.apple.springboard.attemptactivation";
    public static string BrickState => "com.apple.mobile.lockdown.brick_state";
    public static string DeveloperImageMounted => "com.apple.mobile.developer_image_mounted";
    public static string DeviceNameChanged => "com.apple.mobile.lockdown.device_name_changed";
    public static string DiskUsageChanged => "com.apple.mobile.lockdown.disk_usage_changed";
    public static string DsDomainChanged => "com.apple.mobile.data_sync.domain_changed";
    public static string HostAttached => "com.apple.mobile.lockdown.host_attached";
    public static string HostDetached => "com.apple.mobile.lockdown.host_detached";
    public static string ItdbprepDidEnd => "com.apple.itdbprep.notification.didEnd";
    public static string LanguageChanged => "com.apple.language.changed";
    public static string LocalAuthenticationUiDismissed => "com.apple.LocalAuthentication.ui.dismissed";
    public static string LocalAuthenticationUiPresented => "com.apple.LocalAuthentication.ui.presented";
    public static string PhoneNumberChanged => "com.apple.mobile.lockdown.phone_number_changed";
    public static string RegistrationFailed => "com.apple.mobile.lockdown.registration_failed";
    public static string RequestPair => "com.apple.mobile.lockdown.request_pair";
    public static string SyncCancelRequest => "com.apple.itunes-client.syncCancelRequest";
    public static string SyncResumeRequst => "com.apple.itunes-client.syncResumeRequest";
    public static string SyncSuspendRequst => "com.apple.itunes-client.syncSuspendRequest";
    public static string TimezoneChanged => "com.apple.mobile.lockdown.timezone_changed";
    public static string TrustedHostAttached => "com.apple.mobile.lockdown.trusted_host_attached";

    /*
     * Not 100% sure all of these are notifications that you can listen to but is contained in here so
     * that they can be used and if any are proved to work or not work they can be renamed/updated accordingly.
     */
    #region Other Notifications
    [Experimental("NETIMOBILE001")]
    public static string ABAddressBookMeCardChangeDistributedNotification => "ABAddressBookMeCardChangeDistributedNotification";
    [Experimental("NETIMOBILE001")]
    public static string ACDAccountStoreDidChangeNotification => "ACDAccountStoreDidChangeNotification";
    [Experimental("NETIMOBILE001")]
    public static string AFAssistantEnablementDidChangeDarwinNotification => "AFAssistantEnablementDidChangeDarwinNotification";
    [Experimental("NETIMOBILE001")]
    public static string AFLanguageCodeDidChangeDarwinNotification => "AFLanguageCodeDidChangeDarwinNotification";
    [Experimental("NETIMOBILE001")]
    public static string AppleDatePreferencesChangedNotification => "AppleDatePreferencesChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string AppleKeyboardsPreferencesChangedNotification => "AppleKeyboardsPreferencesChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string AppleLanguagePreferencesChangedNotification => "AppleLanguagePreferencesChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string AppleNumberPreferencesChangedNotification => "AppleNumberPreferencesChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string ApplePreferredContentSizeCategoryChangedNotification => "ApplePreferredContentSizeCategoryChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string AppleTimePreferencesChangedNotification => "AppleTimePreferencesChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string BTSettingsHRMConnectedNotification => "BTSettingsHRMConnectedNotification";
    [Experimental("NETIMOBILE001")]
    public static string BYSetupAssistantFinishedDarwinNotification => "BYSetupAssistantFinishedDarwinNotification";
    [Experimental("NETIMOBILE001")]
    public static string CKAccountChangedNotification => "CKAccountChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string CKIdentityUpdateNotification => "CKIdentityUpdateNotification";
    [Experimental("NETIMOBILE001")]
    public static string CNContactStoreDidChangeNotification => "CNContactStoreDidChangeNotification";
    [Experimental("NETIMOBILE001")]
    public static string CNContactStoreLimitedAccessDidChangeNotification => "CNContactStoreLimitedAccessDidChangeNotification";
    [Experimental("NETIMOBILE001")]
    public static string CNContactStoreMeContactDidChangeNotification => "CNContactStoreMeContactDidChangeNotification";
    [Experimental("NETIMOBILE001")]
    public static string CNFavoritesChangedExternallyNotification => "CNFavoritesChangedExternallyNotification";
    [Experimental("NETIMOBILE001")]
    public static string CSLDisableWristDetectionChangedNotification => "CSLDisableWristDetectionChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string CalSyncClientBeginningMultiSave => "CalSyncClientBeginningMultiSave";
    [Experimental("NETIMOBILE001")]
    public static string CalSyncClientFinishedMultiSave => "CalSyncClientFinishedMultiSave";
    [Experimental("NETIMOBILE001")]
    public static string ConnectedGymPreferencesChangedNotification => "ConnectedGymPreferencesChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string EKNotificationCountChangedExternallyNotification => "EKNotificationCountChangedExternallyNotification";
    [Experimental("NETIMOBILE001")]
    public static string FMFDevicesChangedNotification => "FMFDevicesChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string FMFMeDeviceChangedNotification => "FMFMeDeviceChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string FMLDevicesChangedNotification => "FMLDevicesChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string FMLFollowersChangedNotification => "FMLFollowersChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string FMLMeDeviceChangedNotification => "FMLMeDeviceChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string FitnessPlusPlanCoachingDefaultsUpdatedNotification => "FitnessPlusPlanCoachingDefaultsUpdatedNotification";
    [Experimental("NETIMOBILE001")]
    public static string HKHealthDaemonActiveDataCollectionWillStartNotification => "HKHealthDaemonActiveDataCollectionWillStartNotification";
    [Experimental("NETIMOBILE001")]
    public static string HKHealthDaemonActiveWorkoutServersDidUpdateNotification => "HKHealthDaemonActiveWorkoutServersDidUpdateNotification";
    [Experimental("NETIMOBILE001")]
    public static string HKHealthPeripheralStatusDidChangeNotification => "HKHealthPeripheralStatusDidChangeNotification";
    [Experimental("NETIMOBILE001")]
    public static string INVoocabularyChangedNotification => "INVoocabularyChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string MFNanoMailImportantBridgeSettingHasChangedDarwinNotification => "MFNanoMailImportantBridgeSettingHasChangedDarwinNotification";
    [Experimental("NETIMOBILE001")]
    public static string MISProvisioningProfileInstalled => "MISProvisioningProfileInstalled";
    [Experimental("NETIMOBILE001")]
    public static string MISProvisioningProfileRemoved => "MISProvisioningProfileRemoved";
    [Experimental("NETIMOBILE001")]
    public static string MPStoreClientTokenDidChangeNotification => "MPStoreClientTokenDidChangeNotification";
    [Experimental("NETIMOBILE001")]
    public static string NILocalDeviceStartedInteractingWithTokenNotification => "NILocalDeviceStartedInteractingWithTokenNotification";
    [Experimental("NETIMOBILE001")]
    public static string NanoLifestylePreferencesChangedNotification => "NanoLifestylePreferencesChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string NanoLifestylePrivacyPreferencesChangedNotification => "NanoLifestylePrivacyPreferencesChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string NewCarrierNotification => "NewCarrierNotification";
    [Experimental("NETIMOBILE001")]
    public static string NewOperatorNotification => "NewOperatorNotification";
    [Experimental("NETIMOBILE001")]
    public static string NoteContextDarwinNotificationWithLoggedChanges => "NoteContextDarwinNotificationWithLoggedChanges";
    [Experimental("NETIMOBILE001")]
    public static string PCPreferencesDidChangeNotification => "PCPreferencesDidChangeNotification";
    [Experimental("NETIMOBILE001")]
    public static string RTLocationsOfInterestDidChangeNotification => "RTLocationsOfInterestDidChangeNotification";
    [Experimental("NETIMOBILE001")]
    public static string RTLocationsOfInterestDidClearNotification => "RTLocationsOfInterestDidClearNotification";
    [Experimental("NETIMOBILE001")]
    public static string SBApplicationNotificationStateChanged => "SBApplicationNotificationStateChanged";
    [Experimental("NETIMOBILE001")]
    public static string SLSharedWithYouAppSettingHasChanged => "SLSharedWithYouAppSettingHasChanged";
    [Experimental("NETIMOBILE001")]
    public static string SLSharedWithYouSettingHasChanged => "SLSharedWithYouSettingHasChanged";
    [Experimental("NETIMOBILE001")]
    public static string SPAccountRemovedNotification => "SPAccountRemovedNotification";
    [Experimental("NETIMOBILE001")]
    public static string SUPreferencesChangedNotification => "SUPreferencesChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string SeymourWorkoutPlanChanged => "SeymourWorkoutPlanChanged";
    [Experimental("NETIMOBILE001")]
    public static string SignificantTimeChangeNotification => "SignificantTimeChangeNotification";
    [Experimental("NETIMOBILE001")]
    public static string TMRTCResetNotification => "TMRTCResetNotification";
    [Experimental("NETIMOBILE001")]
    public static string UIAccessibilityInvertColorsChanged => "UIAccessibilityInvertColorsChanged";
    [Experimental("NETIMOBILE001")]
    public static string VMStoreSetTokenNotification => "VMStoreSetTokenNotification";
    [Experimental("NETIMOBILE001")]
    public static string VT_Phrase_Type_changed => "VT Phrase Type changed";
    [Experimental("NETIMOBILE001")]
    public static string VVMessageWaitingFallbackNotification => "VVMessageWaitingFallbackNotification";
    [Experimental("NETIMOBILE001")]
    public static string _CDPWalrusStateChangeDarwinNotification => "_CDPWalrusStateChangeDarwinNotification";
    [Experimental("NETIMOBILE001")]
    public static string _CalDatabaseChangedNotification => "_CalDatabaseChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string _CalDatabaseIntegrationDataChangedNotification => "_CalDatabaseIntegrationDataChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string __ABDataBaseChangedByOtherProcessNotification => "__ABDataBaseChangedByOtherProcessNotification";
    [Experimental("NETIMOBILE001")]
    public static string FMIPStateDidChange => "com.apple.AOSNotification.FMIPStateDidChange";
    [Experimental("NETIMOBILE001")]
    public static string device_prevent_playback => "com.apple.AirTunes.DACP.device-prevent-playback";
    [Experimental("NETIMOBILE001")]
    public static string devicevolume => "com.apple.AirTunes.DACP.devicevolume";
    [Experimental("NETIMOBILE001")]
    public static string devicevolumechanged => "com.apple.AirTunes.DACP.devicevolumechanged";
    [Experimental("NETIMOBILE001")]
    public static string mutetoggle => "com.apple.AirTunes.DACP.mutetoggle";
    [Experimental("NETIMOBILE001")]
    public static string nextitem => "com.apple.AirTunes.DACP.nextitem";
    [Experimental("NETIMOBILE001")]
    public static string pause => "com.apple.AirTunes.DACP.pause";
    [Experimental("NETIMOBILE001")]
    public static string play => "com.apple.AirTunes.DACP.play";
    [Experimental("NETIMOBILE001")]
    public static string previtem => "com.apple.AirTunes.DACP.previtem";
    [Experimental("NETIMOBILE001")]
    public static string repeatadv => "com.apple.AirTunes.DACP.repeatadv";
    [Experimental("NETIMOBILE001")]
    public static string shuffletoggle => "com.apple.AirTunes.DACP.shuffletoggle";
    [Experimental("NETIMOBILE001")]
    public static string volumedown => "com.apple.AirTunes.DACP.volumedown";
    [Experimental("NETIMOBILE001")]
    public static string volumeup => "com.apple.AirTunes.DACP.volumeup";
    [Experimental("NETIMOBILE001")]
    public static string dataUpdated => "com.apple.AppleMediaServices.accountCachedData.dataUpdated";
    [Experimental("NETIMOBILE001")]
    public static string deviceOffersChanged => "com.apple.AppleMediaServices.deviceOffersChanged";
    [Experimental("NETIMOBILE001")]
    public static string eligibilityoverridechanged => "com.apple.AppleMediaServices.eligibilityoverridechanged";
    [Experimental("NETIMOBILE001")]
    public static string terminus => "com.apple.ApplicationService.replicatord.terminus";
    [Experimental("NETIMOBILE001")]
    public static string matchOperationStartAttempted => "com.apple.BiometricKit.matchOperationStartAttempted";
    [Experimental("NETIMOBILE001")]
    public static string passcodeGracePeriodChanged => "com.apple.BiometricKit.passcodeGracePeriodChanged";
    [Experimental("NETIMOBILE001")]
    public static string launchnotification => "com.apple.CallHistoryPluginHelper.launchnotification";
    [Experimental("NETIMOBILE001")]
    public static string wristStateChanged => "com.apple.Carousel.wristStateChanged";
    [Experimental("NETIMOBILE001")]
    public static string DonateNow => "com.apple.CascadeSets.DonateNow";
    [Experimental("NETIMOBILE001")]
    public static string CloudSubscriptionFeatureChanged => "com.apple.CloudSubscriptionFeature.Changed";
    [Experimental("NETIMOBILE001")]
    public static string CloudSubscriptionFeaturesOptInChanged => "com.apple.CloudSubscriptionFeatures.OptIn.Changed";
    [Experimental("NETIMOBILE001")]
    public static string enabled => "com.apple.ContinuityKeyBoard.enabled";
    [Experimental("NETIMOBILE001")]
    public static string shutdowsoon => "com.apple.DuetHeuristic-BM.shutdowsoon";
    [Experimental("NETIMOBILE001")]
    public static string record => "com.apple.EscrowSecurityAlert.record";
    [Experimental("NETIMOBILE001")]
    public static string reset => "com.apple.EscrowSecurityAlert.reset";
    [Experimental("NETIMOBILE001")]
    public static string server => "com.apple.EscrowSecurityAlert.server";
    [Experimental("NETIMOBILE001")]
    public static string LocatableStateReported => "com.apple.FindMyDevice.LocatableStateReported";
    [Experimental("NETIMOBILE001")]
    public static string FCPauseRingsSampleChangedNotification => "com.apple.FitnessCoaching.FCPauseRingsSampleChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string SettingsChanged => "com.apple.GeoServices.PreferencesSync.SettingsChanged";
    [Experimental("NETIMOBILE001")]
    public static string started => "com.apple.GeoServices.navigation.started";
    [Experimental("NETIMOBILE001")]
    public static string stopped => "com.apple.GeoServices.navigation.stopped";
    [Experimental("NETIMOBILE001")]
    public static string pairedDeviceExperimentsConfigChanged => "com.apple.GeoServices.pairedDeviceExperimentsConfigChanged";
    [Experimental("NETIMOBILE001")]
    public static string ReportFactoryInstall => "com.apple.InstallerDiagnostics.ReportFactoryInstall";
    [Experimental("NETIMOBILE001")]
    public static string ApplicationsChanged => "com.apple.LaunchServices.ApplicationsChanged";
    [Experimental("NETIMOBILE001")]
    public static string applicationRegistered => "com.apple.LaunchServices.applicationRegistered";
    [Experimental("NETIMOBILE001")]
    public static string applicationUnregistered => "com.apple.LaunchServices.applicationUnregistered";
    [Experimental("NETIMOBILE001")]
    public static string database => "com.apple.LaunchServices.database";
    [Experimental("NETIMOBILE001")]
    public static string StateDidChange => "com.apple.LocalAuthentication.ratchet.StateDidChange";
    [Experimental("NETIMOBILE001")]
    public static string accountChanged => "com.apple.LockdownMode.accountChanged";
    [Experimental("NETIMOBILE001")]
    public static string isLoggedIn => "com.apple.LoginKit.isLoggedIn";
    [Experimental("NETIMOBILE001")]
    public static string _managementStatusChangedForDomains => "com.apple.MCX._managementStatusChangedForDomains";
    [Experimental("NETIMOBILE001")]
    public static string longerstringtarget => "com.apple.ManagedClient.ActivationLockAllowedStateDidChange";
    [Experimental("NETIMOBILE001")]
    public static string ActivationLockAllowedStateDidChange => "com.apple.ManagedConfiguration.managedAppsChanged";
    [Experimental("NETIMOBILE001")]
    public static string profileListChanged => "com.apple.ManagedConfiguration.profileListChanged";
    [Experimental("NETIMOBILE001")]
    public static string webContentFilterChanged => "com.apple.ManagedConfiguration.webContentFilterChanged";
    [Experimental("NETIMOBILE001")]
    public static string webContentFilterTypeChanged => "com.apple.ManagedConfiguration.webContentFilterTypeChanged";
    [Experimental("NETIMOBILE001")]
    public static string lockScreenControlsDidChange => "com.apple.MediaRemote.lockScreenControlsDidChange";
    [Experimental("NETIMOBILE001")]
    public static string nowPlayingActivePlayersIsPlayingDidChange => "com.apple.MediaRemote.nowPlayingActivePlayersIsPlayingDidChange";
    [Experimental("NETIMOBILE001")]
    public static string nowPlayingApplicationIsPlayingDidChange => "com.apple.MediaRemote.nowPlayingApplicationIsPlayingDidChange";
    [Experimental("NETIMOBILE001")]
    public static string nowPlayingInfoDidChange => "com.apple.MediaRemote.nowPlayingInfoDidChange";
    [Experimental("NETIMOBILE001")]
    public static string new_asset_installed => "com.apple.MobileAsset.AppleKeyServicesCRL.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string ATOMIC_INSTANCE_DOWNLOADED => "com.apple.MobileAsset.AutoAssetAtomicNotification^ATOMIC_INSTANCE_DOWNLOADED";
    [Experimental("NETIMOBILE001")]
    public static string ATOMIC_INSTANCE_ELIMINATED => "com.apple.MobileAsset.AutoAssetAtomicNotification^ATOMIC_INSTANCE_ELIMINATED";
    [Experimental("NETIMOBILE001")]
    public static string ATOMIC_INSTANCE_NO_ENTRIES => "com.apple.MobileAsset.AutoAssetAtomicNotification^ATOMIC_INSTANCE_NO_ENTRIES";
    [Experimental("NETIMOBILE001")]
    public static string translation_assets_ATOMIC_INSTANCE_DOWNLOADED => "com.apple.MobileAsset.AutoAssetAtomicNotification^com.apple.translation.assets^ATOMIC_INSTANCE_DOWNLOADED";
    [Experimental("NETIMOBILE001")]
    public static string translation_assets_ATOMIC_INSTANCE_ELIMINATED => "com.apple.MobileAsset.AutoAssetAtomicNotification^com.apple.translation.assets^ATOMIC_INSTANCE_ELIMINATED";
    [Experimental("NETIMOBILE001")]
    public static string translation_assets_ATOMIC_INSTANCE_NO_ENTRIES => "com.apple.MobileAsset.AutoAssetAtomicNotification^com.apple.translation.assets^ATOMIC_INSTANCE_NO_ENTRIES";
    [Experimental("NETIMOBILE001")]
    public static string STARTUP_ACTIVATED => "com.apple.MobileAsset.AutoAssetNotification^com.apple.MobileAsset.MAAutoAsset^STARTUP_ACTIVATED";
    [Experimental("NETIMOBILE001")]
    public static string ASSET_VERSION_DOWNLOADED => "com.apple.MobileAsset.AutoAssetNotification^com.apple.MobileAsset.OSEligibility^ASSET_VERSION_DOWNLOADED";
    [Experimental("NETIMOBILE001")]
    public static string cached_metadata_updated => "com.apple.MobileAsset.CoreTextAssets.ma.cached-metadata-updated";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_CoreTextAssets_ma_new_asset_installed => "com.apple.MobileAsset.CoreTextAssets.ma.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_EmbeddedSpeech_ma_new_asset_installed => "com.apple.MobileAsset.EmbeddedSpeech.ma.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_Font7_ma_cached_metadata_updated => "com.apple.MobileAsset.Font7.ma.cached-metadata-updated";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_KextDenyList_ma_new_asset_installed => "com.apple.MobileAsset.KextDenyList.ma.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_SecureElementServiceAssets_ma_cached_metadata_updated => "com.apple.MobileAsset.SecureElementServiceAssets.ma.cached-metadata-updated";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_SecureElementServiceAssets_ma_new_asset_installed => "com.apple.MobileAsset.SecureElementServiceAssets.ma.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_SpeechEndpointAssets_cached_metadata_updated => "com.apple.MobileAsset.SpeechEndpointAssets.cached-metadata-updated";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_SpeechEndpointAssets_ma_cached_metadata_updated => "com.apple.MobileAsset.SpeechEndpointAssets.ma.cached-metadata-updated";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_TTSAXResourceModelAssets_ma_new_asset_installed => "com.apple.MobileAsset.TTSAXResourceModelAssets.ma.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_TimeZoneUpdate_ma_cached_metadata_updated => "com.apple.MobileAsset.TimeZoneUpdate.ma.cached-metadata-updated";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_TimeZoneUpdate_manew_asset_installed => "com.apple.MobileAsset.TimeZoneUpdate.ma.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceServices_CustomVoice_ma_new_asset_installed => "com.apple.MobileAsset.VoiceServices.CustomVoice.ma.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceServices_GryphonVoice_ma_new_asset_installed => "com.apple.MobileAsset.VoiceServices.GryphonVoice.ma.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceServices_VoiceResources_ma_new_asset_installed => "com.apple.MobileAsset.VoiceServices.VoiceResources.ma.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceServices_VoiceResources_new_asset_installed => "com.apple.MobileAsset.VoiceServices.VoiceResources.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceServicesVocalizerVoice_ma_cached_metadata_updated => "com.apple.MobileAsset.VoiceServicesVocalizerVoice.ma.cached-metadata-updated";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerAssets_cached_metadata_updated => "com.apple.MobileAsset.VoiceTriggerAssets.cached-metadata-updated";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerAssets_ma_cached_metadata_updated => "com.apple.MobileAsset.VoiceTriggerAssets.ma.cached-metadata-updated";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerAssets_ma_new_asset_installed => "com.apple.MobileAsset.VoiceTriggerAssets.ma.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerAssets_new_asset_installed => "com.apple.MobileAsset.VoiceTriggerAssets.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerAssetsIPad_ma_cached_metadata_updated => "com.apple.MobileAsset.VoiceTriggerAssetsIPad.ma.cached-metadata-updated";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerAssetsIPad_ma_new_asset_installed => "com.apple.MobileAsset.VoiceTriggerAssetsIPad.ma.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerAssetsMarsh_ma_cached_metadata_updated => "com.apple.MobileAsset.VoiceTriggerAssetsMarsh.ma.cached-metadata-updated";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerAssetsMarsh_ma_new_asset_installed => "com.apple.MobileAsset.VoiceTriggerAssetsMarsh.ma.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerAssetsStudioDisplay_ma_cached_metadata_updated => "com.apple.MobileAsset.VoiceTriggerAssetsStudioDisplay.ma.cached-metadata-updated";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerAssetsStudioDisplay_ma_new_asset_installed => "com.apple.MobileAsset.VoiceTriggerAssetsStudioDisplay.ma.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerAssetsWatch_ma_cached_metadata_updated => "com.apple.MobileAsset.VoiceTriggerAssetsWatch.ma.cached-metadata-updated";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerAssetsWatch_ma_new_asset_installed => "com.apple.MobileAsset.VoiceTriggerAssetsWatch.ma.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerAssetsWatch_new_asset_installed => "com.apple.MobileAsset.VoiceTriggerAssetsWatch.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerHSAssets_ma_cached_metadata_updated => "com.apple.MobileAsset.VoiceTriggerHSAssets.ma.cached-metadata-updated";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerHSAssets_ma_new_asset_installed => "com.apple.MobileAsset.VoiceTriggerHSAssets.ma.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerHSAssetsIPad_ma_cached_metadata_updated => "com.apple.MobileAsset.VoiceTriggerHSAssetsIPad.ma.cached-metadata-updated";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerHSAssetsIPad_ma_new_asset_installed => "com.apple.MobileAsset.VoiceTriggerHSAssetsIPad.ma.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerHSAssetsWatch_ma_cached_metadata_updated => "com.apple.MobileAsset.VoiceTriggerHSAssetsWatch.ma.cached-metadata-updated";
    [Experimental("NETIMOBILE001")]
    public static string MobileAsset_VoiceTriggerHSAssetsWatch_ma_new_asset_installed => "com.apple.MobileAsset.VoiceTriggerHSAssetsWatch.ma.new-asset-installed";
    [Experimental("NETIMOBILE001")]
    public static string backgroundCellularAccessChanged => "com.apple.MobileBackup.backgroundCellularAccessChanged";
    [Experimental("NETIMOBILE001")]
    public static string OSVersionChanged => "com.apple.MobileSoftwareUpdate.OSVersionChanged";
    [Experimental("NETIMOBILE001")]
    public static string Music_AllowsCellularDataDownloads => "com.apple.Music-AllowsCellularDataDownloads";
    [Experimental("NETIMOBILE001")]
    public static string changed => "com.apple.NanoPhotos.Library.changed";
    [Experimental("NETIMOBILE001")]
    public static string SubmissionPreferenceChanged => "com.apple.OTACrashCopier.SubmissionPreferenceChanged";
    [Experimental("NETIMOBILE001")]
    public static string ChangedRestrictionsEnabledStateNotification => "com.apple.Preferences.ChangedRestrictionsEnabledStateNotification";
    [Experimental("NETIMOBILE001")]
    public static string ResetPrivacyWarningsNotification => "com.apple.Preferences.ResetPrivacyWarningsNotification";
    [Experimental("NETIMOBILE001")]
    public static string mobileBackupStateChange => "com.apple.ProtectedCloudStorage.mobileBackupStateChange";
    [Experimental("NETIMOBILE001")]
    public static string rollBackupDisabled => "com.apple.ProtectedCloudStorage.rollBackupDisabled";
    [Experimental("NETIMOBILE001")]
    public static string rollIfAged => "com.apple.ProtectedCloudStorage.rollIfAged";
    [Experimental("NETIMOBILE001")]
    public static string rollNow => "com.apple.ProtectedCloudStorage.rollNow";
    [Experimental("NETIMOBILE001")]
    public static string ProtectedCloudStorage_test_mobileBackupStateChange => "com.apple.ProtectedCloudStorage.test.mobileBackupStateChange";
    [Experimental("NETIMOBILE001")]
    public static string updatedKeys => "com.apple.ProtectedCloudStorage.updatedKeys";
    [Experimental("NETIMOBILE001")]
    public static string LockScreenDiscovery => "com.apple.ProximityControl.LockScreenDiscovery";
    [Experimental("NETIMOBILE001")]
    public static string SOSNotifyContactsReasonCinnamon => "com.apple.SOSEngine.SOSNotifyContactsReasonCinnamon";
    [Experimental("NETIMOBILE001")]
    public static string SOSNotifyContactsReasonKappa => "com.apple.SOSEngine.SOSNotifyContactsReasonKappa";
    [Experimental("NETIMOBILE001")]
    public static string SOSNotifyContactsReasonMandrake => "com.apple.SOSEngine.SOSNotifyContactsReasonMandrake";
    [Experimental("NETIMOBILE001")]
    public static string SOSNotifyContactsReasonNewton => "com.apple.SOSEngine.SOSNotifyContactsReasonNewton";
    [Experimental("NETIMOBILE001")]
    public static string SOSNotifyContactsReasonSOSTrigger => "com.apple.SOSEngine.SOSNotifyContactsReasonSOSTrigger";
    [Experimental("NETIMOBILE001")]
    public static string reload_plugin => "com.apple.SafariShared.Assistant.reload_plugin";
    [Experimental("NETIMOBILE001")]
    public static string received => "com.apple.SafeEjectGPUStartupDaemon.received";
    [Experimental("NETIMOBILE001")]
    public static string als => "com.apple.SensorKit.als";
    [Experimental("NETIMOBILE001")]
    public static string deviceUsageReport => "com.apple.SensorKit.deviceUsageReport";
    [Experimental("NETIMOBILE001")]
    public static string mediaEvents => "com.apple.SensorKit.mediaEvents";
    [Experimental("NETIMOBILE001")]
    public static string messagesUsageReport => "com.apple.SensorKit.messagesUsageReport";
    [Experimental("NETIMOBILE001")]
    public static string phoneUsageReport => "com.apple.SensorKit.phoneUsageReport";
    [Experimental("NETIMOBILE001")]
    public static string visits => "com.apple.SensorKit.visits";
    [Experimental("NETIMOBILE001")]
    public static string prefsChanged => "com.apple.Sharing.prefsChanged";
    [Experimental("NETIMOBILE001")]
    public static string cancelled => "com.apple.SiriTTSTrainingAgent.taskEvent.cancelled";
    [Experimental("NETIMOBILE001")]
    public static string done => "com.apple.SiriTTSTrainingAgent.taskEvent.done";
    [Experimental("NETIMOBILE001")]
    public static string taskEvent_event => "com.apple.SiriTTSTrainingAgent.taskEvent.event";
    [Experimental("NETIMOBILE001")]
    public static string failed => "com.apple.SiriTTSTrainingAgent.taskEvent.failed";
    [Experimental("NETIMOBILE001")]
    public static string running => "com.apple.SiriTTSTrainingAgent.taskEvent.running";
    [Experimental("NETIMOBILE001")]
    public static string submitted => "com.apple.SiriTTSTrainingAgent.taskEvent.submitted";
    [Experimental("NETIMOBILE001")]
    public static string undefined => "com.apple.SiriTTSTrainingAgent.taskEvent.undefined";
    [Experimental("NETIMOBILE001")]
    public static string CheckForCatalogChange => "com.apple.SoftwareUpdate.CheckForCatalogChange";
    [Experimental("NETIMOBILE001")]
    public static string SUPreferencesChanged => "com.apple.SoftwareUpdate.SUPreferencesChanged";
    [Experimental("NETIMOBILE001")]
    public static string TriggerBackgroundCheck => "com.apple.SoftwareUpdate.TriggerBackgroundCheck";
    [Experimental("NETIMOBILE001")]
    public static string activeaccountchanged => "com.apple.StoreServices.SSAccountStore.activeaccountchanged";
    [Experimental("NETIMOBILE001")]
    public static string StorefrontChanged => "com.apple.StoreServices.StorefrontChanged";
    [Experimental("NETIMOBILE001")]
    public static string updatedVoices => "com.apple.SynthesisProvider.updatedVoices";
    [Experimental("NETIMOBILE001")]
    public static string connectionRequested => "com.apple.TVRemoteCore.connectionRequested";
    [Experimental("NETIMOBILE001")]
    public static string IdleScreenRefreshIntervalChanged => "com.apple.TVScreenSaver.IdleScreenRefreshIntervalChanged";
    [Experimental("NETIMOBILE001")]
    public static string IdleScreenScreenSaverTypeChanged => "com.apple.TVScreenSaver.IdleScreenScreenSaverTypeChanged";
    [Experimental("NETIMOBILE001")]
    public static string PhotosSharingFilterChanged => "com.apple.TVScreenSaver.PhotosSharingFilterChanged";
    [Experimental("NETIMOBILE001")]
    public static string RequestMemoriesRefresh => "com.apple.TVScreenSaver.RequestMemoriesRefresh";
    [Experimental("NETIMOBILE001")]
    public static string TVScreenSaverAssetServiceManagerUpdated => "com.apple.TVScreenSaver.TVScreenSaverAssetServiceManagerUpdated";
    [Experimental("NETIMOBILE001")]
    public static string DefaultAppRelayTelephonySettingChanged => "com.apple.TelephonyUtilities.DefaultAppRelayTelephonySettingChanged";
    [Experimental("NETIMOBILE001")]
    public static string RemoveAllDynamicDictionariesNotification => "com.apple.TextInput.RemoveAllDynamicDictionariesNotification";
    [Experimental("NETIMOBILE001")]
    public static string application => "com.apple.UsageTrackingAgent.registration.application";
    [Experimental("NETIMOBILE001")]
    public static string now_playing => "com.apple.UsageTrackingAgent.registration.now-playing";
    [Experimental("NETIMOBILE001")]
    public static string video => "com.apple.UsageTrackingAgent.registration.video";
    [Experimental("NETIMOBILE001")]
    public static string web_domain => "com.apple.UsageTrackingAgent.registration.web-domain";
    [Experimental("NETIMOBILE001")]
    public static string ProfileStoreDidUpdate => "com.apple.UserProfiles.ProfileStoreDidUpdate";
    [Experimental("NETIMOBILE001")]
    public static string DidRegisterSubscription => "com.apple.VideoSubscriberAccount.DidRegisterSubscription";
    [Experimental("NETIMOBILE001")]
    public static string PlayHistoryUpdatedNotification => "com.apple.VideosUI.PlayHistoryUpdatedNotification";
    [Experimental("NETIMOBILE001")]
    public static string StoreAcquisitionCrossProcessNotification => "com.apple.VideosUI.StoreAcquisitionCrossProcessNotification";
    [Experimental("NETIMOBILE001")]
    public static string UpNextRequestDidFinishNotification => "com.apple.VideosUI.UpNextRequestDidFinishNotification";
    [Experimental("NETIMOBILE001")]
    public static string accessibility_cache_darken_system_colors_enabled => "com.apple.accessibility.cache.darken.system.colors.enabled";
    [Experimental("NETIMOBILE001")]
    public static string color => "com.apple.accessibility.cache.differentiate.without.color";
    [Experimental("NETIMOBILE001")]
    public static string contrast => "com.apple.accessibility.cache.enhance.background.contrast";
    [Experimental("NETIMOBILE001")]
    public static string legibility => "com.apple.accessibility.cache.enhance.text.legibility";
    [Experimental("NETIMOBILE001")]
    public static string colors => "com.apple.accessibility.cache.invert.colors";
    [Experimental("NETIMOBILE001")]
    public static string text => "com.apple.accessibility.cache.prefers.horizontal.text";
    [Experimental("NETIMOBILE001")]
    public static string motion => "com.apple.accessibility.cache.reduce.motion";
    [Experimental("NETIMOBILE001")]
    public static string status => "com.apple.accessibility.classic.wob.status";
    [Experimental("NETIMOBILE001")]
    public static string accessibility_commandandcontrol_status => "com.apple.accessibility.commandandcontrol.status";
    [Experimental("NETIMOBILE001")]
    public static string accessibility_enhance_background_contrast_status => "com.apple.accessibility.enhance.background.contrast.status";
    [Experimental("NETIMOBILE001")]
    public static string accessibility_pointer_increased_contrast => "com.apple.accessibility.pointer.increased.contrast";
    [Experimental("NETIMOBILE001")]
    public static string accessibility_prefers_horizontal_text => "com.apple.accessibility.prefers.horizontal.text";
    [Experimental("NETIMOBILE001")]
    public static string accessibility_reduce_motion_status => "com.apple.accessibility.reduce.motion.status";
    [Experimental("NETIMOBILE001")]
    public static string point => "com.apple.accessibility.reduce.white.point";
    [Experimental("NETIMOBILE001")]
    public static string accessibility_voiceovertouch_status => "com.apple.accessibility.voiceovertouch.status";
    [Experimental("NETIMOBILE001")]
    public static string accessibility_zoomtouch_status => "com.apple.accessibility.zoomtouch.status";
    [Experimental("NETIMOBILE001")]
    public static string MFi4AccessoryDisconnected => "com.apple.accessories.connection.MFi4AccessoryDisconnected";
    [Experimental("NETIMOBILE001")]
    public static string passedMFi4Auth => "com.apple.accessories.connection.passedMFi4Auth";
    [Experimental("NETIMOBILE001")]
    public static string privateListeningChanged => "com.apple.ams.privateListeningChanged";
    [Experimental("NETIMOBILE001")]
    public static string provision_biometrics => "com.apple.ams.provision-biometrics";
    [Experimental("NETIMOBILE001")]
    public static string canceltasks => "com.apple.ap.adprivacyd.canceltasks";
    [Experimental("NETIMOBILE001")]
    public static string deviceKnowledge => "com.apple.ap.adprivacyd.deviceKnowledge";
    [Experimental("NETIMOBILE001")]
    public static string iTunesActiveAccountDidChangeNotification => "com.apple.ap.adprivacyd.iTunesActiveAccountDidChangeNotification";
    [Experimental("NETIMOBILE001")]
    public static string iTunesActiveStorefrontDidChangeNotification => "com.apple.ap.adprivacyd.iTunesActiveStorefrontDidChangeNotification";
    [Experimental("NETIMOBILE001")]
    public static string launch => "com.apple.ap.adprivacyd.launch";
    [Experimental("NETIMOBILE001")]
    public static string reconcile => "com.apple.ap.adprivacyd.reconcile";
    [Experimental("NETIMOBILE001")]
    public static string backgroundstate => "com.apple.appletv.backgroundstate";
    [Experimental("NETIMOBILE001")]
    public static string message => "com.apple.appplaceholdersyncd.replicatorclient.message";
    [Experimental("NETIMOBILE001")]
    public static string appplaceholdersyncd_replicatorclient_record => "com.apple.appplaceholdersyncd.replicatorclient.record";
    [Experimental("NETIMOBILE001")]
    public static string change => "com.apple.appprotection.change";
    [Experimental("NETIMOBILE001")]
    public static string hidden => "com.apple.appprotection.change.hidden";
    [Experimental("NETIMOBILE001")]
    public static string locked => "com.apple.appprotection.change.locked";
    [Experimental("NETIMOBILE001")]
    public static string ActivitySubEntitlementsCacheUpdated => "com.apple.appstored.ActivitySubEntitlementsCacheUpdated";
    [Experimental("NETIMOBILE001")]
    public static string AppStoreSubEntitlementsCacheUpdated => "com.apple.appstored.AppStoreSubEntitlementsCacheUpdated";
    [Experimental("NETIMOBILE001")]
    public static string HWBundleSubEntitlementsCacheUpdated => "com.apple.appstored.HWBundleSubEntitlementsCacheUpdated";
    [Experimental("NETIMOBILE001")]
    public static string MusicSubEntitlementsCacheUpdated => "com.apple.appstored.MusicSubEntitlementsCacheUpdated";
    [Experimental("NETIMOBILE001")]
    public static string NewsSubEntitlementsCacheUpdated => "com.apple.appstored.NewsSubEntitlementsCacheUpdated";
    [Experimental("NETIMOBILE001")]
    public static string PodcastSubEntitlementsCacheUpdated => "com.apple.appstored.PodcastSubEntitlementsCacheUpdated";
    [Experimental("NETIMOBILE001")]
    public static string TVSubEntitlementsCacheUpdated => "com.apple.appstored.TVSubEntitlementsCacheUpdated";
    [Experimental("NETIMOBILE001")]
    public static string iCloudSubEntitlementsCacheUpdated => "com.apple.appstored.iCloudSubEntitlementsCacheUpdated";
    [Experimental("NETIMOBILE001")]
    public static string app_vocabulary => "com.apple.assistant.app_vocabulary";
    [Experimental("NETIMOBILE001")]
    public static string didChange => "com.apple.assistant.domain.didChange";
    [Experimental("NETIMOBILE001")]
    public static string assistant_domain_preferences_didChange => "com.apple.assistant.domain.preferences.didChange";
    [Experimental("NETIMOBILE001")]
    public static string assistant_domain_priority_didChange => "com.apple.assistant.domain.priority.didChange";
    [Experimental("NETIMOBILE001")]
    public static string siri_settings_did_change => "com.apple.assistant.siri_settings_did_change";
    [Experimental("NETIMOBILE001")]
    public static string finished => "com.apple.assistant.speech-capture.finished";
    [Experimental("NETIMOBILE001")]
    public static string sync_data_changed => "com.apple.assistant.sync_data_changed";
    [Experimental("NETIMOBILE001")]
    public static string sync_homekit_now => "com.apple.assistant.sync_homekit_now";
    [Experimental("NETIMOBILE001")]
    public static string sync_homekit_urgent => "com.apple.assistant.sync_homekit_urgent";
    [Experimental("NETIMOBILE001")]
    public static string sync_needed => "com.apple.assistant.sync_needed";
    [Experimental("NETIMOBILE001")]
    public static string runkeeplocaltask => "com.apple.atc.xpc.runkeeplocaltask";
    [Experimental("NETIMOBILE001")]
    public static string enable => "com.apple.audio.AOP.enable";
    [Experimental("NETIMOBILE001")]
    public static string wifi => "com.apple.awd.launch.wifi";
    [Experimental("NETIMOBILE001")]
    public static string ConfigChange => "com.apple.awdd.ConfigChange";
    [Experimental("NETIMOBILE001")]
    public static string anonymity => "com.apple.awdd.anonymity";
    [Experimental("NETIMOBILE001")]
    public static string WirelessSplitterOn => "com.apple.bluetooth.WirelessSplitterOn";
    [Experimental("NETIMOBILE001")]
    public static string success => "com.apple.bluetooth.accessory-authentication.success";
    [Experimental("NETIMOBILE001")]
    public static string connection => "com.apple.bluetooth.connection";
    [Experimental("NETIMOBILE001")]
    public static string daemonStarted => "com.apple.bluetooth.daemonStarted";
    [Experimental("NETIMOBILE001")]
    public static string pairing => "com.apple.bluetooth.pairing";
    [Experimental("NETIMOBILE001")]
    public static string pairingWithReason => "com.apple.bluetooth.pairingWithReason";
    [Experimental("NETIMOBILE001")]
    public static string state => "com.apple.bluetooth.state";
    [Experimental("NETIMOBILE001")]
    public static string BookmarksFileChanged => "com.apple.bookmarks.BookmarksFileChanged";
    [Experimental("NETIMOBILE001")]
    public static string kCalPreferredDaysToSyncKey => "com.apple.calendar.database.preference.notification.kCalPreferredDaysToSyncKey";
    [Experimental("NETIMOBILE001")]
    public static string suggestEventLocations => "com.apple.calendar.database.preference.notification.suggestEventLocations";
    [Experimental("NETIMOBILE001")]
    public static string RecentsClearedNotification => "com.apple.callhistory.RecentsClearedNotification";
    [Experimental("NETIMOBILE001")]
    public static string calls_changed => "com.apple.callhistory.notification.calls-changed";
    [Experimental("NETIMOBILE001")]
    public static string idslaunchnotification => "com.apple.callhistorysync.idslaunchnotification";
    [Experimental("NETIMOBILE001")]
    public static string identificationentrieschanged => "com.apple.callkit.calldirectorymanager.identificationentrieschanged";
    [Experimental("NETIMOBILE001")]
    public static string capabilities_changed => "com.apple.carkit.capabilities-changed";
    [Experimental("NETIMOBILE001")]
    public static string carplay_attached => "com.apple.carkit.carplay-attached";
    [Experimental("NETIMOBILE001")]
    public static string batteryChanged => "com.apple.cddcommunicator.batteryChanged";
    [Experimental("NETIMOBILE001")]
    public static string nwchanged => "com.apple.cddcommunicator.nwchanged";
    [Experimental("NETIMOBILE001")]
    public static string pluginChanged => "com.apple.cddcommunicator.pluginChanged";
    [Experimental("NETIMOBILE001")]
    public static string thermalChanged => "com.apple.cddcommunicator.thermalChanged";
    [Experimental("NETIMOBILE001")]
    public static string siri_data_changed => "com.apple.chatkit.groups.siri_data_changed";
    [Experimental("NETIMOBILE001")]
    public static string chronod_replicator_message => "com.apple.chronod.replicator.message";
    [Experimental("NETIMOBILE001")]
    public static string chronod_replicator_record => "com.apple.chronod.replicator.record";
    [Experimental("NETIMOBILE001")]
    public static string almostfull => "com.apple.cloud.quota.simulate.vfs.almostfull";
    [Experimental("NETIMOBILE001")]
    public static string notfull => "com.apple.cloud.quota.simulate.vfs.notfull";
    [Experimental("NETIMOBILE001")]
    public static string ProactivePredictionsBackup => "com.apple.cloudd.pcsIdentityUpdate-com.apple.ProactivePredictionsBackup";
    [Experimental("NETIMOBILE001")]
    public static string kvstorechange => "com.apple.cloudrecents.kvstorechange";
    [Experimental("NETIMOBILE001")]
    public static string cmfsyncagent_kvstorechange => "com.apple.cmfsyncagent.kvstorechange";
    [Experimental("NETIMOBILE001")]
    public static string storedidchangeexternally => "com.apple.cmfsyncagent.storedidchangeexternally";
    [Experimental("NETIMOBILE001")]
    public static string attach_notification => "com.apple.cmio.VDCAssistant.attach-notification";
    [Experimental("NETIMOBILE001")]
    public static string DataSettingsChangedNotification => "com.apple.commcenter.DataSettingsChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string commcenter_InternationalRoamingEDGE_changed => "com.apple.commcenter.InternationalRoamingEDGE.changed";
    [Experimental("NETIMOBILE001")]
    public static string clientDidDisplayFavorites => "com.apple.contacts.clientDidDisplayFavorites";
    [Experimental("NETIMOBILE001")]
    public static string BorealisToggled => "com.apple.coreaudio.BorealisToggled";
    [Experimental("NETIMOBILE001")]
    public static string IORunning => "com.apple.coreaudio.IORunning";
    [Experimental("NETIMOBILE001")]
    public static string RoutingConfiguration => "com.apple.coreaudio.RoutingConfiguration";
    [Experimental("NETIMOBILE001")]
    public static string borealisTrigger => "com.apple.coreaudio.borealisTrigger";
    [Experimental("NETIMOBILE001")]
    public static string coreaudio_components_changed => "com.apple.coreaudio.components.changed";
    [Experimental("NETIMOBILE001")]
    public static string created => "com.apple.coreaudio.speechDetectionVAD.created";
    [Experimental("NETIMOBILE001")]
    public static string coreduetd => "com.apple.coreduet.client-needs-help.coreduetd";
    [Experimental("NETIMOBILE001")]
    public static string coreduet_idslaunchnotification => "com.apple.coreduet.idslaunchnotification";
    [Experimental("NETIMOBILE001")]
    public static string duetexpertd => "com.apple.coreduetd.knowledgebase.launch.duetexpertd";
    [Experimental("NETIMOBILE001")]
    public static string nearbydeviceschanged => "com.apple.coreduetd.nearbydeviceschanged";
    [Experimental("NETIMOBILE001")]
    public static string remoteDeviceChange => "com.apple.coreduetd.remoteDeviceChange";
    [Experimental("NETIMOBILE001")]
    public static string GUIConsoleSessionChanged => "com.apple.coregraphics.GUIConsoleSessionChanged";
    [Experimental("NETIMOBILE001")]
    public static string carplayisconnected => "com.apple.coremedia.carplayisconnected";
    [Experimental("NETIMOBILE001")]
    public static string iCloudAccountChanged => "com.apple.corerecents.iCloudAccountChanged";
    [Experimental("NETIMOBILE001")]
    public static string ReindexAllItems => "com.apple.corespotlight.developer.ReindexAllItems";
    [Experimental("NETIMOBILE001")]
    public static string ReindexAllItemsWithIdentifiers => "com.apple.corespotlight.developer.ReindexAllItemsWithIdentifiers";
    [Experimental("NETIMOBILE001")]
    public static string auto_submit_preference_changed => "com.apple.crashreporter.auto_submit_preference_changed";
    [Experimental("NETIMOBILE001")]
    public static string tasking_changed => "com.apple.da.tasking_changed";
    [Experimental("NETIMOBILE001")]
    public static string checkHolidayCalendarAccount => "com.apple.dataaccess.checkHolidayCalendarAccount";
    [Experimental("NETIMOBILE001")]
    public static string ping => "com.apple.dataaccess.ping";
    [Experimental("NETIMOBILE001")]
    public static string datamigrationcompletecontinuerestore => "com.apple.datamigrator.datamigrationcompletecontinuerestore";
    [Experimental("NETIMOBILE001")]
    public static string migrationDidFinish => "com.apple.datamigrator.migrationDidFinish";
    [Experimental("NETIMOBILE001")]
    public static string devicePostureChanged => "com.apple.devicemanagementclient.devicePostureChanged";
    [Experimental("NETIMOBILE001")]
    public static string longLivedTokenChanged => "com.apple.devicemanagementclient.longLivedTokenChanged";
    [Experimental("NETIMOBILE001")]
    public static string dmd_budget_didChange => "com.apple.dmd.budget.didChange";
    [Experimental("NETIMOBILE001")]
    public static string dmd_iCloudAccount_didChange => "com.apple.dmd.iCloudAccount.didChange";
    [Experimental("NETIMOBILE001")]
    public static string appRefresh => "com.apple.duet.expertcenter.appRefresh";
    [Experimental("NETIMOBILE001")]
    public static string internalSettingsChanged => "com.apple.duetbm.internalSettingsChanged";
    [Experimental("NETIMOBILE001")]
    public static string BluetoothConnectedAnchor => "com.apple.duetexpertd.ATXAnchorModel.BluetoothConnectedAnchor";
    [Experimental("NETIMOBILE001")]
    public static string CarPlayConnectedAnchor => "com.apple.duetexpertd.ATXAnchorModel.CarPlayConnectedAnchor";
    [Experimental("NETIMOBILE001")]
    public static string ChargerConnectedAnchor => "com.apple.duetexpertd.ATXAnchorModel.ChargerConnectedAnchor";
    [Experimental("NETIMOBILE001")]
    public static string IdleTimeEndAnchor => "com.apple.duetexpertd.ATXAnchorModel.IdleTimeEndAnchor";
    [Experimental("NETIMOBILE001")]
    public static string WiredAudioDeviceConnectedAnchor => "com.apple.duetexpertd.ATXAnchorModel.WiredAudioDeviceConnectedAnchor";
    [Experimental("NETIMOBILE001")]
    public static string duetexpertd_ATXAnchorModel_invalidate_BluetoothConnectedAnchor => "com.apple.duetexpertd.ATXAnchorModel.invalidate.BluetoothConnectedAnchor";
    [Experimental("NETIMOBILE001")]
    public static string duetexpertd_ATXAnchorModel_invalidate_CarPlayConnectedAnchor => "com.apple.duetexpertd.ATXAnchorModel.invalidate.CarPlayConnectedAnchor";
    [Experimental("NETIMOBILE001")]
    public static string duetexpertd_ATXAnchorModel_invalidate_ChargerConnectedAnchor => "com.apple.duetexpertd.ATXAnchorModel.invalidate.ChargerConnectedAnchor";
    [Experimental("NETIMOBILE001")]
    public static string duetexpertd_ATXAnchorModel_invalidate_IdleTimeEndAnchor => "com.apple.duetexpertd.ATXAnchorModel.invalidate.IdleTimeEndAnchor";
    [Experimental("NETIMOBILE001")]
    public static string duetexpertd_ATXAnchorModel_invalidate_WiredAudioDeviceConnectedAnchor => "com.apple.duetexpertd.ATXAnchorModel.invalidate.WiredAudioDeviceConnectedAnchor";
    [Experimental("NETIMOBILE001")]
    public static string duetexpertd_ATXMMAppPredictor_BluetoothConnectedAnchor => "com.apple.duetexpertd.ATXMMAppPredictor.BluetoothConnectedAnchor";
    [Experimental("NETIMOBILE001")]
    public static string BluetoothDisconnectedAnchor => "com.apple.duetexpertd.ATXMMAppPredictor.BluetoothDisconnectedAnchor";
    [Experimental("NETIMOBILE001")]
    public static string duetexpertd_ATXMMAppPredictor_CarPlayConnectedAnchor => "com.apple.duetexpertd.ATXMMAppPredictor.CarPlayConnectedAnchor";
    [Experimental("NETIMOBILE001")]
    public static string CarPlayDisconnectedAnchor => "com.apple.duetexpertd.ATXMMAppPredictor.CarPlayDisconnectedAnchor";
    [Experimental("NETIMOBILE001")]
    public static string duetexpertd_ATXMMAppPredictor_IdleTimeEndAnchor => "com.apple.duetexpertd.ATXMMAppPredictor.IdleTimeEndAnchor";
    [Experimental("NETIMOBILE001")]
    public static string duetexpertd_ATXMMAppPredictor_WiredAudioDeviceConnectedAnchor => "com.apple.duetexpertd.ATXMMAppPredictor.WiredAudioDeviceConnectedAnchor";
    [Experimental("NETIMOBILE001")]
    public static string WiredAudioDeviceDisconnectedAnchor => "com.apple.duetexpertd.ATXMMAppPredictor.WiredAudioDeviceDisconnectedAnchor";
    [Experimental("NETIMOBILE001")]
    public static string ATXScreenUnlockUpdateSource => "com.apple.duetexpertd.ATXScreenUnlockUpdateSource";
    [Experimental("NETIMOBILE001")]
    public static string appchangeprediction => "com.apple.duetexpertd.appchangeprediction";
    [Experimental("NETIMOBILE001")]
    public static string appclipprediction => "com.apple.duetexpertd.appclipprediction";
    [Experimental("NETIMOBILE001")]
    public static string clientModelRefreshBlendingLayer => "com.apple.duetexpertd.clientModelRefreshBlendingLayer";
    [Experimental("NETIMOBILE001")]
    public static string defaultsChanged => "com.apple.duetexpertd.defaultsChanged";
    [Experimental("NETIMOBILE001")]
    public static string dockAppListCacheUpdate => "com.apple.duetexpertd.dockAppListCacheUpdate";
    [Experimental("NETIMOBILE001")]
    public static string activity => "com.apple.duetexpertd.donationmonitor.activity";
    [Experimental("NETIMOBILE001")]
    public static string intent => "com.apple.duetexpertd.donationmonitor.intent";
    [Experimental("NETIMOBILE001")]
    public static string feedbackavailable => "com.apple.duetexpertd.feedbackavailable";
    [Experimental("NETIMOBILE001")]
    public static string homeScreenPageConfigCacheUpdate => "com.apple.duetexpertd.homeScreenPageConfigCacheUpdate";
    [Experimental("NETIMOBILE001")]
    public static string audiodisconnect => "com.apple.duetexpertd.mm.audiodisconnect";
    [Experimental("NETIMOBILE001")]
    public static string bluetoothconnected => "com.apple.duetexpertd.mm.bluetoothconnected";
    [Experimental("NETIMOBILE001")]
    public static string bluetoothdisconnect => "com.apple.duetexpertd.mm.bluetoothdisconnect";
    [Experimental("NETIMOBILE001")]
    public static string carplayconnect => "com.apple.duetexpertd.ms.carplayconnect";
    [Experimental("NETIMOBILE001")]
    public static string carplaydisconnect => "com.apple.duetexpertd.ms.carplaydisconnect";
    [Experimental("NETIMOBILE001")]
    public static string nowplayingpause => "com.apple.duetexpertd.ms.nowplayingpause";
    [Experimental("NETIMOBILE001")]
    public static string nowplayingplay => "com.apple.duetexpertd.ms.nowplayingplay";
    [Experimental("NETIMOBILE001")]
    public static string prefschanged => "com.apple.duetexpertd.prefschanged";
    [Experimental("NETIMOBILE001")]
    public static string sportsTeamsChanged => "com.apple.duetexpertd.sportsTeamsChanged";
    [Experimental("NETIMOBILE001")]
    public static string updateDefaultsDueToRelevantHomeScreenConfigUpdate => "com.apple.duetexpertd.updateDefaultsDueToRelevantHomeScreenConfigUpdate";
    [Experimental("NETIMOBILE001")]
    public static string AlertInviteeDeclines => "com.apple.eventkit.preference.notification.AlertInviteeDeclines";
    [Experimental("NETIMOBILE001")]
    public static string UnselectedCalendarIdentifiersForFocusMode => "com.apple.eventkit.preference.notification.UnselectedCalendarIdentifiersForFocusMode";
    [Experimental("NETIMOBILE001")]
    public static string exchangesyncd_ping => "com.apple.exchangesyncd.ping";
    [Experimental("NETIMOBILE001")]
    public static string resync_fpkeybag => "com.apple.fairplayd.resync-fpkeybag";
    [Experimental("NETIMOBILE001")]
    public static string family_updated => "com.apple.family.family_updated";
    [Experimental("NETIMOBILE001")]
    public static string FitnessAppInstalled => "com.apple.fitness.FitnessAppInstalled";
    [Experimental("NETIMOBILE001")]
    public static string gamepolicy_daemon_launch => "com.apple.gamepolicy.daemon.launch";
    [Experimental("NETIMOBILE001")]
    public static string apple_geoservices_siri_data_changed => "com.apple.geoservices.siri_data_changed";
    [Experimental("NETIMOBILE001")]
    public static string notification => "com.apple.gms.availability.notification";
    [Experimental("NETIMOBILE001")]
    public static string notification_private => "com.apple.gms.availability.notification.private";
    [Experimental("NETIMOBILE001")]
    public static string htse_state_changed => "com.apple.hangtracerd.htse_state_changed";
    [Experimental("NETIMOBILE001")]
    public static string SleepDetectedActivity => "com.apple.healthlite.SleepDetectedActivity";
    [Experimental("NETIMOBILE001")]
    public static string SleepSessionEndRequest => "com.apple.healthlite.SleepSessionEndRequest";
    [Experimental("NETIMOBILE001")]
    public static string AppleTVAccessoryAdded => "com.apple.homed.AppleTVAccessoryAdded";
    [Experimental("NETIMOBILE001")]
    public static string multi_user_status_changed => "com.apple.homed.multi-user-status-changed";
    [Experimental("NETIMOBILE001")]
    public static string speakersConfiguredChanged => "com.apple.homed.speakersConfiguredChanged";
    [Experimental("NETIMOBILE001")]
    public static string televisionAccessoryAdded => "com.apple.homed.televisionAccessoryAdded";
    [Experimental("NETIMOBILE001")]
    public static string multiuser => "com.apple.homed.user-cloud-share.repair.wake.com.apple.applemediaservices.multiuser";
    [Experimental("NETIMOBILE001")]
    public static string homed_user_cloud_share_wake_com_apple_applemediaservices_multiuser => "com.apple.homed.user-cloud-share.wake.com.apple.applemediaservices.multiuser";
    [Experimental("NETIMOBILE001")]
    public static string qa => "com.apple.homed.user-cloud-share.wake.com.apple.applemediaservices.multiuser.qa";
    [Experimental("NETIMOBILE001")]
    public static string container => "com.apple.homed.user-cloud-share.wake.com.apple.mediaservicesbroker.container";
    [Experimental("NETIMOBILE001")]
    public static string data => "com.apple.homed.user-cloud-share.wake.com.apple.siri.data";
    [Experimental("NETIMOBILE001")]
    public static string zonesharing => "com.apple.homed.user-cloud-share.wake.com.apple.siri.zonesharing";
    [Experimental("NETIMOBILE001")]
    public static string endpointActivated => "com.apple.homehubd.endpointActivated";
    [Experimental("NETIMOBILE001")]
    public static string endpointDeactivated => "com.apple.homehubd.endpointDeactivated";
    [Experimental("NETIMOBILE001")]
    public static string sync_data_cache_updated => "com.apple.homekit.sync-data-cache-updated";
    [Experimental("NETIMOBILE001")]
    public static string addMagSafeAccessory => "com.apple.icloud.FindMy.addMagSafeAccessory";
    [Experimental("NETIMOBILE001")]
    public static string added => "com.apple.icloud.findmydeviced.findkit.magSafe.added";
    [Experimental("NETIMOBILE001")]
    public static string attach => "com.apple.icloud.findmydeviced.findkit.magSafe.attach";
    [Experimental("NETIMOBILE001")]
    public static string detach => "com.apple.icloud.findmydeviced.findkit.magSafe.detach";
    [Experimental("NETIMOBILE001")]
    public static string removed => "com.apple.icloud.findmydeviced.findkit.magSafe.removed";
    [Experimental("NETIMOBILE001")]
    public static string localActivationLockInfoChanged => "com.apple.icloud.findmydeviced.localActivationLockInfoChanged";
    [Experimental("NETIMOBILE001")]
    public static string icloud_fmip_lostmode_enable => "com.apple.icloud.fmip.lostmode.enable";
    [Experimental("NETIMOBILE001")]
    public static string icloud_fmip_siri_data_changed => "com.apple.icloud.fmip.siri_data_changed";
    [Experimental("NETIMOBILE001")]
    public static string accessoryDidPair => "com.apple.icloud.searchparty.accessoryDidPair";
    [Experimental("NETIMOBILE001")]
    public static string selfbeaconchanged => "com.apple.icloud.searchparty.selfbeaconchanged";
    [Experimental("NETIMOBILE001")]
    public static string icloudpairing_idslaunchnotification => "com.apple.icloudpairing.idslaunchnotification";
    [Experimental("NETIMOBILE001")]
    public static string idscredentials_idslaunchnotification => "com.apple.idscredentials.idslaunchnotification";
    [Experimental("NETIMOBILE001")]
    public static string idsremoteurlconnection_idslaunchnotification => "com.apple.idsremoteurlconnection.idslaunchnotification";
    [Experimental("NETIMOBILE001")]
    public static string idstransfers_idslaunchnotification => "com.apple.idstransfers.idslaunchnotification";
    [Experimental("NETIMOBILE001")]
    public static string prefchange => "com.apple.imautomatichistorydeletionagent.prefchange";
    [Experimental("NETIMOBILE001")]
    public static string Recovered => "com.apple.intelligenceplatform.StorageSystem.Recovered";
    [Experimental("NETIMOBILE001")]
    public static string displayStatus => "com.apple.iokit.hid.displayStatus";
    [Experimental("NETIMOBILE001")]
    public static string backcamerapower => "com.apple.isp.backcamerapower";
    [Experimental("NETIMOBILE001")]
    public static string frontcamerapower => "com.apple.isp.frontcamerapower";
    [Experimental("NETIMOBILE001")]
    public static string artworkDownloadsDidCompleteNotification => "com.apple.itunescloudd.artworkDownloadsDidCompleteNotification";
    [Experimental("NETIMOBILE001")]
    public static string accountschanged => "com.apple.itunesstored.accountschanged";
    [Experimental("NETIMOBILE001")]
    public static string autodownloaddefaultschange => "com.apple.itunesstored.autodownloaddefaultschange";
    [Experimental("NETIMOBILE001")]
    public static string invalidatebags => "com.apple.itunesstored.invalidatebags";
    [Experimental("NETIMOBILE001")]
    public static string complete => "com.apple.jett.switch.environmentChange.idms.complete";
    [Experimental("NETIMOBILE001")]
    public static string effaced => "com.apple.keystore.memento.effaced";
    [Experimental("NETIMOBILE001")]
    public static string LockdownMode => "com.apple.kvs.store-did-change.com.apple.LockdownMode";
    [Experimental("NETIMOBILE001")]
    public static string livespeech => "com.apple.kvs.store-did-change.com.apple.accessibility.livespeech";
    [Experimental("NETIMOBILE001")]
    public static string settings => "com.apple.kvs.store-did-change.com.apple.bluetooth.cloud.settings";
    [Experimental("NETIMOBILE001")]
    public static string appearance => "com.apple.kvs.store-did-change.com.apple.cloudsettings.appearance";
    [Experimental("NETIMOBILE001")]
    public static string controlcenter => "com.apple.kvs.store-did-change.com.apple.cloudsettings.controlcenter";
    [Experimental("NETIMOBILE001")]
    public static string desktop => "com.apple.kvs.store-did-change.com.apple.cloudsettings.desktop";
    [Experimental("NETIMOBILE001")]
    public static string displays => "com.apple.kvs.store-did-change.com.apple.cloudsettings.displays";
    [Experimental("NETIMOBILE001")]
    public static string gamecontroller => "com.apple.kvs.store-did-change.com.apple.cloudsettings.gamecontroller";
    [Experimental("NETIMOBILE001")]
    public static string general => "com.apple.kvs.store-did-change.com.apple.cloudsettings.general";
    [Experimental("NETIMOBILE001")]
    public static string international => "com.apple.kvs.store-did-change.com.apple.cloudsettings.international";
    [Experimental("NETIMOBILE001")]
    public static string keyboard => "com.apple.kvs.store-did-change.com.apple.cloudsettings.keyboard";
    [Experimental("NETIMOBILE001")]
    public static string mouse => "com.apple.kvs.store-did-change.com.apple.cloudsettings.mouse";
    [Experimental("NETIMOBILE001")]
    public static string pencil => "com.apple.kvs.store-did-change.com.apple.cloudsettings.pencil";
    [Experimental("NETIMOBILE001")]
    public static string sound => "com.apple.kvs.store-did-change.com.apple.cloudsettings.sound";
    [Experimental("NETIMOBILE001")]
    public static string trackpad => "com.apple.kvs.store-did-change.com.apple.cloudsettings.trackpad";
    [Experimental("NETIMOBILE001")]
    public static string iBooks => "com.apple.kvs.store-did-change.com.apple.iBooks";
    [Experimental("NETIMOBILE001")]
    public static string reminders => "com.apple.kvs.store-did-change.com.apple.reminders";
    [Experimental("NETIMOBILE001")]
    public static string sleepd => "com.apple.kvs.store-did-change.com.apple.sleepd";
    [Experimental("NETIMOBILE001")]
    public static string language_changed => "com.apple.language.changed";
    [Experimental("NETIMOBILE001")]
    public static string liveactivitiesd_replicatorParticipant_message => "com.apple.liveactivitiesd.replicatorParticipant.message";
    [Experimental("NETIMOBILE001")]
    public static string liveactivitiesd_replicatorParticipant_record => "com.apple.liveactivitiesd.replicatorParticipant.record";
    [Experimental("NETIMOBILE001")]
    public static string localprefschanged => "com.apple.livespeech.localprefschanged";
    [Experimental("NETIMOBILE001")]
    public static string appreset => "com.apple.locationd.appreset";
    [Experimental("NETIMOBILE001")]
    public static string authorization => "com.apple.locationd.authorization";
    [Experimental("NETIMOBILE001")]
    public static string bufferedDevicesReceived => "com.apple.locationd.gathering.bufferedDevicesReceived";
    [Experimental("NETIMOBILE001")]
    public static string came_up => "com.apple.locationd.gathering.came_up";
    [Experimental("NETIMOBILE001")]
    public static string connected => "com.apple.locationd.vehicle.connected";
    [Experimental("NETIMOBILE001")]
    public static string disconnected => "com.apple.locationd.vehicle.disconnected";
    [Experimental("NETIMOBILE001")]
    public static string exit => "com.apple.locationd.vehicle.exit";
    [Experimental("NETIMOBILE001")]
    public static string toVehicular => "com.apple.locationd.vehicular.changed.toVehicular";
    [Experimental("NETIMOBILE001")]
    public static string Prefs => "com.apple.locationd/Prefs";
    [Experimental("NETIMOBILE001")]
    public static string allowpasscodemodificationchanged => "com.apple.managedconfiguration.allowpasscodemodificationchanged";
    [Experimental("NETIMOBILE001")]
    public static string effectivesettingschanged => "com.apple.managedconfiguration.effectivesettingschanged";
    [Experimental("NETIMOBILE001")]
    public static string managedorginfochanged => "com.apple.managedconfiguration.managedorginfochanged";
    [Experimental("NETIMOBILE001")]
    public static string passcodechanged => "com.apple.managedconfiguration.passcodechanged";
    [Experimental("NETIMOBILE001")]
    public static string restrictionchanged => "com.apple.managedconfiguration.restrictionchanged";
    [Experimental("NETIMOBILE001")]
    public static string media_entities_siri_data_changed => "com.apple.media.entities.siri_data_changed";
    [Experimental("NETIMOBILE001")]
    public static string media_podcasts_siri_data_changed => "com.apple.media.podcasts.siri_data_changed";
    [Experimental("NETIMOBILE001")]
    public static string displayFilterSettingsChanged => "com.apple.mediaaccessibility.displayFilterSettingsChanged";
    [Experimental("NETIMOBILE001")]
    public static string disk_image_mounted => "com.apple.mobile.disk_image_mounted";
    [Experimental("NETIMOBILE001")]
    public static string first_unlock => "com.apple.mobile.keybagd.first_unlock";
    [Experimental("NETIMOBILE001")]
    public static string lock_status => "com.apple.mobile.keybagd.lock_status";
    [Experimental("NETIMOBILE001")]
    public static string BonjourPairingServiceChanged => "com.apple.mobile.lockdown.BonjourPairingServiceChanged";
    [Experimental("NETIMOBILE001")]
    public static string BonjourServiceChanged => "com.apple.mobile.lockdown.BonjourServiceChanged";
    [Experimental("NETIMOBILE001")]
    public static string activation_state => "com.apple.mobile.lockdown.activation_state";
    [Experimental("NETIMOBILE001")]
    public static string device_name_changed => "com.apple.mobile.lockdown.device_name_changed";
    [Experimental("NETIMOBILE001")]
    public static string host_paired => "com.apple.mobile.lockdown.host_paired";
    [Experimental("NETIMOBILE001")]
    public static string trusted_host_attached => "com.apple.mobile.lockdown.trusted_host_attached";
    [Experimental("NETIMOBILE001")]
    public static string storage_unmounted => "com.apple.mobile.storage_unmounted";
    [Experimental("NETIMOBILE001")]
    public static string invitationalertschanged => "com.apple.mobilecal.invitationalertschanged";
    [Experimental("NETIMOBILE001")]
    public static string calendarsExcludedFromNotifications => "com.apple.mobilecal.preference.notification.calendarsExcludedFromNotifications";
    [Experimental("NETIMOBILE001")]
    public static string overlayCalendarID => "com.apple.mobilecal.preference.notification.overlayCalendarID";
    [Experimental("NETIMOBILE001")]
    public static string weekStart => "com.apple.mobilecal.preference.notification.weekStart";
    [Experimental("NETIMOBILE001")]
    public static string timezonechanged => "com.apple.mobilecal.timezonechanged";
    [Experimental("NETIMOBILE001")]
    public static string mobileipod_prefsChanged => "com.apple.mobileipod-prefsChanged";
    [Experimental("NETIMOBILE001")]
    public static string displayvalueschanged => "com.apple.mobileipod.displayvalueschanged";
    [Experimental("NETIMOBILE001")]
    public static string keeplocalstatechanged => "com.apple.mobileipod.keeplocalstatechanged";
    [Experimental("NETIMOBILE001")]
    public static string librarychanged => "com.apple.mobileipod.librarychanged";
    [Experimental("NETIMOBILE001")]
    public static string libraryimportdidfinish => "com.apple.mobileipod.libraryimportdidfinish";
    [Experimental("NETIMOBILE001")]
    public static string noncontentspropertieschanged => "com.apple.mobileipod.noncontentspropertieschanged";
    [Experimental("NETIMOBILE001")]
    public static string poll => "com.apple.mobilemail.afc.poll";
    [Experimental("NETIMOBILE001")]
    public static string allowFindMyFriendsModification => "com.apple.mobileme.fmf1.allowFindMyFriendsModification";
    [Experimental("NETIMOBILE001")]
    public static string refreshui => "com.apple.mobilerepair.refreshui";
    [Experimental("NETIMOBILE001")]
    public static string ICPLStateChanged => "com.apple.mobileslideshow.ICPLStateChanged";
    [Experimental("NETIMOBILE001")]
    public static string PLNotificationKeepOriginalsChanged => "com.apple.mobileslideshow.PLNotificationKeepOriginalsChanged";
    [Experimental("NETIMOBILE001")]
    public static string bedtimetest => "com.apple.mobiletimerd.bedtimetest";
    [Experimental("NETIMOBILE001")]
    public static string chargetest => "com.apple.mobiletimerd.chargetest";
    [Experimental("NETIMOBILE001")]
    public static string diagnostics => "com.apple.mobiletimerd.diagnostics";
    [Experimental("NETIMOBILE001")]
    public static string goodmorningtest => "com.apple.mobiletimerd.goodmorningtest";
    [Experimental("NETIMOBILE001")]
    public static string mobiletimerd_reset => "com.apple.mobiletimerd.reset";
    [Experimental("NETIMOBILE001")]
    public static string resttest => "com.apple.mobiletimerd.resttest";
    [Experimental("NETIMOBILE001")]
    public static string waketest => "com.apple.mobiletimerd.waketest";
    [Experimental("NETIMOBILE001")]
    public static string wakeuptest => "com.apple.mobiletimerd.wakeuptest";
    [Experimental("NETIMOBILE001")]
    public static string generative_experiences_readiness => "com.apple.modelcatalog.generative-experiences-readiness";
    [Experimental("NETIMOBILE001")]
    public static string defaults => "com.apple.nanomusic.sync.defaults";
    [Experimental("NETIMOBILE001")]
    public static string LibraryCollectionTargetMapData_changed => "com.apple.nanophotos.prefs.LibraryCollectionTargetMapData-changed";
    [Experimental("NETIMOBILE001")]
    public static string devicedidpair => "com.apple.nanoregistry.devicedidpair";
    [Experimental("NETIMOBILE001")]
    public static string devicedidunpair => "com.apple.nanoregistry.devicedidunpair";
    [Experimental("NETIMOBILE001")]
    public static string initialSyncDidComplete => "com.apple.nanoregistry.pairedSync.initialSyncDidComplete";
    [Experimental("NETIMOBILE001")]
    public static string paireddevicedidchangecapabilities => "com.apple.nanoregistry.paireddevicedidchangecapabilities";
    [Experimental("NETIMOBILE001")]
    public static string paireddevicedidchangeversion => "com.apple.nanoregistry.paireddevicedidchangeversion";
    [Experimental("NETIMOBILE001")]
    public static string watchdidbecomeactive => "com.apple.nanoregistry.watchdidbecomeactive";
    [Experimental("NETIMOBILE001")]
    public static string startPredicting => "com.apple.navd.backgroundCommute.startPredicting";
    [Experimental("NETIMOBILE001")]
    public static string wakeUpForHypothesisUpdate => "com.apple.navd.wakeUpForHypothesisUpdate";
    [Experimental("NETIMOBILE001")]
    public static string terminal => "com.apple.nearfield.handoff.terminal";
    [Experimental("NETIMOBILE001")]
    public static string app_paths_changed => "com.apple.networkextension.app-paths-changed";
    [Experimental("NETIMOBILE001")]
    public static string apps_changed => "com.apple.networkextension.apps-changed";
    [Experimental("NETIMOBILE001")]
    public static string nehelper_init => "com.apple.networkextension.nehelper-init";
    [Experimental("NETIMOBILE001")]
    public static string phs => "com.apple.networkrelay.launch.phs";
    [Experimental("NETIMOBILE001")]
    public static string networkserviceproxy_reset => "com.apple.networkserviceproxy.reset";
    [Experimental("NETIMOBILE001")]
    public static string nfcacd_multitag_state_change => "com.apple.nfcacd.multitag.state.change";
    [Experimental("NETIMOBILE001")]
    public static string os_eligibility_domain_change => "com.apple.os-eligibility-domain.change";
    [Experimental("NETIMOBILE001")]
    public static string aluminum => "com.apple.os-eligibility-domain.change.aluminum";
    [Experimental("NETIMOBILE001")]
    public static string chromium => "com.apple.os-eligibility-domain.change.chromium";
    [Experimental("NETIMOBILE001")]
    public static string greymatter => "com.apple.os-eligibility-domain.change.greymatter";
    [Experimental("NETIMOBILE001")]
    public static string manganese => "com.apple.os-eligibility-domain.change.manganese";
    [Experimental("NETIMOBILE001")]
    public static string silicon => "com.apple.os-eligibility-domain.change.silicon";
    [Experimental("NETIMOBILE001")]
    public static string input_needed => "com.apple.os-eligibility-domain.input-needed";
    [Experimental("NETIMOBILE001")]
    public static string syncDidComplete => "com.apple.pairedsync.syncDidComplete";
    [Experimental("NETIMOBILE001")]
    public static string FLUploadImmediately => "com.apple.parsec-fbf.FLUploadImmediately";
    [Experimental("NETIMOBILE001")]
    public static string bag => "com.apple.parsecd.bag";
    [Experimental("NETIMOBILE001")]
    public static string clearData => "com.apple.parsecd.queries.clearData";
    [Experimental("NETIMOBILE001")]
    public static string pasteboard_notify_changed => "com.apple.pasteboard.notify.changed";
    [Experimental("NETIMOBILE001")]
    public static string focalappchanged => "com.apple.pex.connections.focalappchanged";
    [Experimental("NETIMOBILE001")]
    public static string DidUpdateAutonamingUserFeedback => "com.apple.photos.DidUpdateAutonamingUserFeedback";
    [Experimental("NETIMOBILE001")]
    public static string network_service => "com.apple.photosface.network-service";
    [Experimental("NETIMOBILE001")]
    public static string photostream_idslaunchnotification => "com.apple.photostream.idslaunchnotification";
    [Experimental("NETIMOBILE001")]
    public static string batteryServiceNotification => "com.apple.powerlog.batteryServiceNotification";
    [Experimental("NETIMOBILE001")]
    public static string idlesleeppreventers => "com.apple.powermanagement.idlesleeppreventers";
    [Experimental("NETIMOBILE001")]
    public static string restartpreventers => "com.apple.powermanagement.restartpreventers";
    [Experimental("NETIMOBILE001")]
    public static string systempowerstate => "com.apple.powermanagement.systempowerstate";
    [Experimental("NETIMOBILE001")]
    public static string systemsleeppreventers => "com.apple.powermanagement.systemsleeppreventers";
    [Experimental("NETIMOBILE001")]
    public static string requiredFullCharge => "com.apple.powerui.requiredFullCharge";
    [Experimental("NETIMOBILE001")]
    public static string smartcharge => "com.apple.powerui.smartcharge";
    [Experimental("NETIMOBILE001")]
    public static string stridecalibration => "com.apple.private.SensorKit.pedometer.stridecalibration";
    [Experimental("NETIMOBILE001")]
    public static string private_restrict_post_MobileBackup_backgroundCellularAccessChanged => "com.apple.private.restrict-post.MobileBackup.backgroundCellularAccessChanged";
    [Experimental("NETIMOBILE001")]
    public static string namedEntitiesInvalidated => "com.apple.proactive.PersonalizationPortrait.namedEntitiesInvalidated";
    [Experimental("NETIMOBILE001")]
    public static string weather => "com.apple.proactive.information.source.weather";
    [Experimental("NETIMOBILE001")]
    public static string proactive_queries_clearData => "com.apple.proactive.queries.clearData";
    [Experimental("NETIMOBILE001")]
    public static string databaseChange => "com.apple.proactive.queries.databaseChange";
    [Experimental("NETIMOBILE001")]
    public static string setupdone => "com.apple.purplebuddy.setupdone";
    [Experimental("NETIMOBILE001")]
    public static string setupexited => "com.apple.purplebuddy.setupexited";
    [Experimental("NETIMOBILE001")]
    public static string pushproxy_idslaunchnotification => "com.apple.pushproxy.idslaunchnotification";
    [Experimental("NETIMOBILE001")]
    public static string CompanionLinkDeviceAdded => "com.apple.rapport.CompanionLinkDeviceAdded";
    [Experimental("NETIMOBILE001")]
    public static string CompanionLinkDeviceRemoved => "com.apple.rapport.CompanionLinkDeviceRemoved";
    [Experimental("NETIMOBILE001")]
    public static string rapport_prefsChanged => "com.apple.rapport.prefsChanged";
    [Experimental("NETIMOBILE001")]
    public static string nano_preferences_sync => "com.apple.remindd.nano_preferences_sync";
    [Experimental("NETIMOBILE001")]
    public static string storeChanged => "com.apple.reminderkit.storeChanged";
    [Experimental("NETIMOBILE001")]
    public static string accountsChanged => "com.apple.remotemanagement.accountsChanged";
    [Experimental("NETIMOBILE001")]
    public static string remotepairingdevice_host_paired => "com.apple.remotepairingdevice.host_paired";
    [Experimental("NETIMOBILE001")]
    public static string devicesChanged => "com.apple.replicatord.devicesChanged";
    [Experimental("NETIMOBILE001")]
    public static string hipuncap => "com.apple.request.hipuncap";
    [Experimental("NETIMOBILE001")]
    public static string sbd_kvstorechange => "com.apple.sbd.kvstorechange";
    [Experimental("NETIMOBILE001")]
    public static string screensharing_idslaunchnotification => "com.apple.screensharing.idslaunchnotification";
    [Experimental("NETIMOBILE001")]
    public static string forceupdate => "com.apple.security.cloudkeychain.forceupdate";
    [Experimental("NETIMOBILE001")]
    public static string kvstorechange3 => "com.apple.security.cloudkeychainproxy.kvstorechange3";
    [Experimental("NETIMOBILE001")]
    public static string itembackup => "com.apple.security.itembackup";
    [Experimental("NETIMOBILE001")]
    public static string groupsupdated => "com.apple.security.kcsharing.groupsupdated";
    [Experimental("NETIMOBILE001")]
    public static string joined_with_bottle => "com.apple.security.octagon.joined-with-bottle";
    [Experimental("NETIMOBILE001")]
    public static string peer_changed => "com.apple.security.octagon.peer-changed";
    [Experimental("NETIMOBILE001")]
    public static string trust_status_change => "com.apple.security.octagon.trust-status-change";
    [Experimental("NETIMOBILE001")]
    public static string publickeyavailable => "com.apple.security.publickeyavailable";
    [Experimental("NETIMOBILE001")]
    public static string publickeynotavailable => "com.apple.security.publickeynotavailable";
    [Experimental("NETIMOBILE001")]
    public static string circlechanged => "com.apple.security.secureobjectsync.circlechanged";
    [Experimental("NETIMOBILE001")]
    public static string holdlock => "com.apple.security.secureobjectsync.holdlock";
    [Experimental("NETIMOBILE001")]
    public static string viewschanged => "com.apple.security.secureobjectsync.viewschanged";
    [Experimental("NETIMOBILE001")]
    public static string PCS => "com.apple.security.view-change.PCS";
    [Experimental("NETIMOBILE001")]
    public static string security_view_change_SE_PTC => "com.apple.security.view-change.SE-PTC";
    [Experimental("NETIMOBILE001")]
    public static string security_view_ready_SE_PTC => "com.apple.security.view-ready.SE-PTC";
    [Experimental("NETIMOBILE001")]
    public static string screenIsLocked => "com.apple.sessionagent.screenIsLocked";
    [Experimental("NETIMOBILE001")]
    public static string screenIsUnlocked => "com.apple.sessionagent.screenIsUnlocked";
    [Experimental("NETIMOBILE001")]
    public static string daemon_wakeup_request => "com.apple.shortcuts.daemon-wakeup-request";
    [Experimental("NETIMOBILE001")]
    public static string runner_prewarm_request => "com.apple.shortcuts.runner-prewarm-request";
    [Experimental("NETIMOBILE001")]
    public static string ShortcutsCloudKitAccountAddedNotification => "com.apple.siri.ShortcutsCloudKitAccountAddedNotification";
    [Experimental("NETIMOBILE001")]
    public static string ShortcutsCloudKitAccountModifiedNotification => "com.apple.siri.ShortcutsCloudKitAccountModifiedNotification";
    [Experimental("NETIMOBILE001")]
    public static string DataDidUpdateNotification => "com.apple.siri.VoiceShortcuts.DataDidUpdateNotification";
    [Experimental("NETIMOBILE001")]
    public static string siri_client_state_DynamiteClientState_siri_data_changed => "com.apple.siri.client.state.DynamiteClientState.siri_data_changed";
    [Experimental("NETIMOBILE001")]
    public static string deleted => "com.apple.siri.cloud.storage.deleted";
    [Experimental("NETIMOBILE001")]
    public static string siri_cloud_synch_changed => "com.apple.siri.cloud.synch.changed";
    [Experimental("NETIMOBILE001")]
    public static string requested => "com.apple.siri.history.deletion.requested";
    [Experimental("NETIMOBILE001")]
    public static string audio_app_signals_update => "com.apple.siri.inference.audio-app-signals-update";
    [Experimental("NETIMOBILE001")]
    public static string donate => "com.apple.siri.koa.donate";
    [Experimental("NETIMOBILE001")]
    public static string updated => "com.apple.siri.power.PowerContextPolicy.updated";
    [Experimental("NETIMOBILE001")]
    public static string quiet => "com.apple.siri.preheat.quiet";
    [Experimental("NETIMOBILE001")]
    public static string Overrides => "com.apple.siri.uaf.com.apple.MobileAsset.UAF.FM.Overrides";
    [Experimental("NETIMOBILE001")]
    public static string root => "com.apple.siri.uaf.com.apple.MobileAsset.UAF.FM.Overrides.root";
    [Experimental("NETIMOBILE001")]
    public static string Visual => "com.apple.siri.uaf.com.apple.MobileAsset.UAF.FM.Visual";
    [Experimental("NETIMOBILE001")]
    public static string modelcatalog => "com.apple.siri.uaf.com.apple.modelcatalog";
    [Experimental("NETIMOBILE001")]
    public static string modelcatalog_root => "com.apple.siri.uaf.com.apple.modelcatalog.root";
    [Experimental("NETIMOBILE001")]
    public static string understanding => "com.apple.siri.uaf.com.apple.siri.understanding";
    [Experimental("NETIMOBILE001")]
    public static string overrides => "com.apple.siri.uaf.com.apple.siri.understanding.nl.overrides";
    [Experimental("NETIMOBILE001")]
    public static string automaticspeechrecognition => "com.apple.siri.uaf.com.apple.speech.automaticspeechrecognition";
    [Experimental("NETIMOBILE001")]
    public static string perception => "com.apple.siri.uaf.com.apple.voiceassistant.perception";
    [Experimental("NETIMOBILE001")]
    public static string contacts_changed => "com.apple.siri.vocabulary.contacts_changed";
    [Experimental("NETIMOBILE001")]
    public static string SleepRecordDidChange => "com.apple.sleep.sync.SleepRecordDidChange";
    [Experimental("NETIMOBILE001")]
    public static string SleepScheduleDidChange => "com.apple.sleep.sync.SleepScheduleDidChange";
    [Experimental("NETIMOBILE001")]
    public static string SleepSettingsDidChange => "com.apple.sleep.sync.SleepSettingsDidChange";
    [Experimental("NETIMOBILE001")]
    public static string analytics => "com.apple.sleepd.analytics";
    [Experimental("NETIMOBILE001")]
    public static string cloudkit_reset => "com.apple.sleepd.cloudkit.reset";
    [Experimental("NETIMOBILE001")]
    public static string sleepd_diagnostics => "com.apple.sleepd.diagnostics";
    [Experimental("NETIMOBILE001")]
    public static string test => "com.apple.sleepd.ids.test";
    [Experimental("NETIMOBILE001")]
    public static string defaultschanged => "com.apple.smartcharging.defaultschanged";
    [Experimental("NETIMOBILE001")]
    public static string sockpuppet_applications_updated => "com.apple.sockpuppet.applications.updated";
    [Experimental("NETIMOBILE001")]
    public static string startInstall => "com.apple.softwareupdate.autoinstall.startInstall";
    [Experimental("NETIMOBILE001")]
    public static string SUCoreConfigScheduledScan => "com.apple.softwareupdateservicesd.SUCoreConfigScheduledScan";
    [Experimental("NETIMOBILE001")]
    public static string autoDownload => "com.apple.softwareupdateservicesd.activity.autoDownload";
    [Experimental("NETIMOBILE001")]
    public static string autoDownloadEnd => "com.apple.softwareupdateservicesd.activity.autoDownloadEnd";
    [Experimental("NETIMOBILE001")]
    public static string autoInstallEnd => "com.apple.softwareupdateservicesd.activity.autoInstallEnd";
    [Experimental("NETIMOBILE001")]
    public static string autoInstallUnlock => "com.apple.softwareupdateservicesd.activity.autoInstallUnlock";
    [Experimental("NETIMOBILE001")]
    public static string autoScan => "com.apple.softwareupdateservicesd.activity.autoScan";
    [Experimental("NETIMOBILE001")]
    public static string delayEndScan => "com.apple.softwareupdateservicesd.activity.delayEndScan";
    [Experimental("NETIMOBILE001")]
    public static string emergencyAutoScan => "com.apple.softwareupdateservicesd.activity.emergencyAutoScan";
    [Experimental("NETIMOBILE001")]
    public static string installAlert => "com.apple.softwareupdateservicesd.activity.installAlert";
    [Experimental("NETIMOBILE001")]
    public static string presentBanner => "com.apple.softwareupdateservicesd.activity.presentBanner";
    [Experimental("NETIMOBILE001")]
    public static string rollbackReboot => "com.apple.softwareupdateservicesd.activity.rollbackReboot";
    [Experimental("NETIMOBILE001")]
    public static string splatAutoScan => "com.apple.softwareupdateservicesd.activity.splatAutoScan";
    [Experimental("NETIMOBILE001")]
    public static string SyndicatedContentDeleted => "com.apple.spotlight.SyndicatedContentDeleted";
    [Experimental("NETIMOBILE001")]
    public static string SyndicatedContentRefreshed => "com.apple.spotlight.SyndicatedContentRefreshed";
    [Experimental("NETIMOBILE001")]
    public static string spotlightui_prefschanged => "com.apple.spotlightui.prefschanged";
    [Experimental("NETIMOBILE001")]
    public static string finishedstartup => "com.apple.springboard.finishedstartup";
    [Experimental("NETIMOBILE001")]
    public static string hasBlankedScreen => "com.apple.springboard.hasBlankedScreen";
    [Experimental("NETIMOBILE001")]
    public static string lockstate => "com.apple.springboard.lockstate";
    [Experimental("NETIMOBILE001")]
    public static string pluggedin => "com.apple.springboard.pluggedin";
    [Experimental("NETIMOBILE001")]
    public static string mfd => "com.apple.stockholm.se.mfd";
    [Experimental("NETIMOBILE001")]
    public static string prepareForQuery => "com.apple.suggestions.prepareForQuery";
    [Experimental("NETIMOBILE001")]
    public static string settingsChanged => "com.apple.suggestions.settingsChanged";
    [Experimental("NETIMOBILE001")]
    public static string materialLinkQualityChange => "com.apple.symptoms.materialLinkQualityChange";
    [Experimental("NETIMOBILE001")]
    public static string sysdiagnoseStarted => "com.apple.sysdiagnose.sysdiagnoseStarted";
    [Experimental("NETIMOBILE001")]
    public static string sysdiagnoseStopped => "com.apple.sysdiagnose.sysdiagnoseStopped";
    [Experimental("NETIMOBILE001")]
    public static string accpowersources_attach => "com.apple.system.accpowersources.attach";
    [Experimental("NETIMOBILE001")]
    public static string source => "com.apple.system.accpowersources.source";
    [Experimental("NETIMOBILE001")]
    public static string clock_set => "com.apple.system.clock_set";
    [Experimental("NETIMOBILE001")]
    public static string network_change => "com.apple.system.config.network_change";
    [Experimental("NETIMOBILE001")]
    public static string hostname => "com.apple.system.hostname";
    [Experimental("NETIMOBILE001")]
    public static string power_button_notification => "com.apple.system.logging.power_button_notification";
    [Experimental("NETIMOBILE001")]
    public static string desktopUp => "com.apple.system.loginwindow.desktopUp";
    [Experimental("NETIMOBILE001")]
    public static string system => "com.apple.system.lowdiskspace.system";
    [Experimental("NETIMOBILE001")]
    public static string lowpowermode => "com.apple.system.lowpowermode";
    [Experimental("NETIMOBILE001")]
    public static string auto_disabled => "com.apple.system.lowpowermode.auto_disabled";
    [Experimental("NETIMOBILE001")]
    public static string first_time => "com.apple.system.lowpowermode.first_time";
    [Experimental("NETIMOBILE001")]
    public static string poweradapter => "com.apple.system.powermanagement.poweradapter";
    [Experimental("NETIMOBILE001")]
    public static string useractivity2 => "com.apple.system.powermanagement.useractivity2";
    [Experimental("NETIMOBILE001")]
    public static string uservisiblepowerevent => "com.apple.system.powermanagement.uservisiblepowerevent";
    [Experimental("NETIMOBILE001")]
    public static string criticallevel => "com.apple.system.powersources.criticallevel";
    [Experimental("NETIMOBILE001")]
    public static string percent => "com.apple.system.powersources.percent";
    [Experimental("NETIMOBILE001")]
    public static string powersources_source => "com.apple.system.powersources.source";
    [Experimental("NETIMOBILE001")]
    public static string timeremaining => "com.apple.system.powersources.timeremaining";
    [Experimental("NETIMOBILE001")]
    public static string thermalpressurelevel => "com.apple.system.thermalpressurelevel";
    [Experimental("NETIMOBILE001")]
    public static string cold => "com.apple.system.thermalpressurelevel.cold";
    [Experimental("NETIMOBILE001")]
    public static string timezone => "com.apple.system.timezone";
    [Experimental("NETIMOBILE001")]
    public static string verylowdiskspace_system => "com.apple.system.verylowdiskspace.system";
    [Experimental("NETIMOBILE001")]
    public static string tcc_access_changed => "com.apple.tcc.access.changed";
    [Experimental("NETIMOBILE001")]
    public static string fakeincomingmessage => "com.apple.telephonyutilities.callservicesd.fakeincomingmessage";
    [Experimental("NETIMOBILE001")]
    public static string fakeoutgoingmessage => "com.apple.telephonyutilities.callservicesd.fakeoutgoingmessage";
    [Experimental("NETIMOBILE001")]
    public static string voicemailcallended => "com.apple.telephonyutilities.callservicesdaemon.voicemailcallended";
    [Experimental("NETIMOBILE001")]
    public static string ageAwareMitigationsEnabled => "com.apple.thermalmonitor.ageAwareMitigationsEnabled";
    [Experimental("NETIMOBILE001")]
    public static string timezoneprefschanged => "com.apple.timezone.prefschanged";
    [Experimental("NETIMOBILE001")]
    public static string timezonesync_idslaunchnotification => "com.apple.timezonesync.idslaunchnotification";
    [Experimental("NETIMOBILE001")]
    public static string touchsetupd_launch => "com.apple.touchsetupd.launch";
    [Experimental("NETIMOBILE001")]
    public static string FREEZER_POLICIES => "com.apple.trial.NamespaceUpdate.FREEZER_POLICIES";
    [Experimental("NETIMOBILE001")]
    public static string NETWORK_SERVICE_PROXY_CONFIG_UPDATE => "com.apple.trial.NamespaceUpdate.NETWORK_SERVICE_PROXY_CONFIG_UPDATE";
    [Experimental("NETIMOBILE001")]
    public static string SIRI_DICTATION_ASSETS => "com.apple.trial.NamespaceUpdate.SIRI_DICTATION_ASSETS";
    [Experimental("NETIMOBILE001")]
    public static string SIRI_TEXT_TO_SPEECH => "com.apple.trial.NamespaceUpdate.SIRI_TEXT_TO_SPEECH";
    [Experimental("NETIMOBILE001")]
    public static string SIRI_UNDERSTANDING_ASR_ASSISTANT => "com.apple.trial.NamespaceUpdate.SIRI_UNDERSTANDING_ASR_ASSISTANT";
    [Experimental("NETIMOBILE001")]
    public static string SIRI_UNDERSTANDING_ATTENTION_ASSETS => "com.apple.trial.NamespaceUpdate.SIRI_UNDERSTANDING_ATTENTION_ASSETS";
    [Experimental("NETIMOBILE001")]
    public static string SIRI_UNDERSTANDING_NL => "com.apple.trial.NamespaceUpdate.SIRI_UNDERSTANDING_NL";
    [Experimental("NETIMOBILE001")]
    public static string SIRI_UNDERSTANDING_NL_OVERRIDES => "com.apple.trial.NamespaceUpdate.SIRI_UNDERSTANDING_NL_OVERRIDES";
    [Experimental("NETIMOBILE001")]
    public static string activated => "com.apple.trial.bmlt.activated";
    [Experimental("NETIMOBILE001")]
    public static string new_experiment => "com.apple.triald.new-experiment";
    [Experimental("NETIMOBILE001")]
    public static string system_wake => "com.apple.triald.system.wake";
    [Experimental("NETIMOBILE001")]
    public static string triald_wake => "com.apple.triald.wake";
    [Experimental("NETIMOBILE001")]
    public static string NewAssetNotification => "com.apple.ttsasset.NewAssetNotification";
    [Experimental("NETIMOBILE001")]
    public static string Register => "com.apple.tv.TVWidgetExtension.Register";
    [Experimental("NETIMOBILE001")]
    public static string appRemoved => "com.apple.tv.appRemoved";
    [Experimental("NETIMOBILE001")]
    public static string updateAppVisibility => "com.apple.tv.updateAppVisibility";
    [Experimental("NETIMOBILE001")]
    public static string BTLEServer_personalizationNeeded => "com.apple.uarp.BTLEServer.personalizationNeeded";
    [Experimental("NETIMOBILE001")]
    public static string UARPUpdaterServiceHID_personalizationNeeded => "com.apple.uarp.UARPUpdaterServiceHID.personalizationNeeded";
    [Experimental("NETIMOBILE001")]
    public static string migrationCompleted => "com.apple.videos.migrationCompleted";
    [Experimental("NETIMOBILE001")]
    public static string ReloadService => "com.apple.voicemail.ReloadService";
    [Experimental("NETIMOBILE001")]
    public static string VVVerifierCheckpointDictionaryChanged => "com.apple.voicemail.VVVerifierCheckpointDictionaryChanged";
    [Experimental("NETIMOBILE001")]
    public static string voicemail_changed => "com.apple.voicemail.changed";
    [Experimental("NETIMOBILE001")]
    public static string voice_update => "com.apple.voiceservices.notification.voice-update";
    [Experimental("NETIMOBILE001")]
    public static string asset_force_update => "com.apple.voiceservices.trigger.asset-force-update";
    [Experimental("NETIMOBILE001")]
    public static string EarlyDetect => "com.apple.voicetrigger.EarlyDetect";
    [Experimental("NETIMOBILE001")]
    public static string PHSProfileModified => "com.apple.voicetrigger.PHSProfileModified";
    [Experimental("NETIMOBILE001")]
    public static string ConnectionChanged => "com.apple.voicetrigger.RemoteDarwin.ConnectionChanged";
    [Experimental("NETIMOBILE001")]
    public static string RemoteDarwin_EarlyDetect => "com.apple.voicetrigger.RemoteDarwin.EarlyDetect";
    [Experimental("NETIMOBILE001")]
    public static string XPCRestarted => "com.apple.voicetrigger.XPCRestarted";
    [Experimental("NETIMOBILE001")]
    public static string enablePolicyChanged => "com.apple.voicetrigger.enablePolicyChanged";
    [Experimental("NETIMOBILE001")]
    public static string wake_up => "com.apple.wcd.wake-up";
    [Experimental("NETIMOBILE001")]
    public static string disabled => "com.apple.webinspectord.disabled";
    [Experimental("NETIMOBILE001")]
    public static string webinspectord_enabled => "com.apple.webinspectord.enabled";
    [Experimental("NETIMOBILE001")]
    public static string dismissed => "com.apple.welcomekitinternalsettings.dismissed";
    [Experimental("NETIMOBILE001")]
    public static string wirelessinsightsd_anonymity => "com.apple.wirelessinsightsd.anonymity";
    [Experimental("NETIMOBILE001")]
    public static string wirelessproximity_launch => "com.apple.wirelessproximity.launch";
    [Experimental("NETIMOBILE001")]
    public static string app => "dmf.policy.monitor.app";
    [Experimental("NETIMOBILE001")]
    public static string kAFPreferencesDidChangeDarwinNotification => "kAFPreferencesDidChangeDarwinNotification";
    [Experimental("NETIMOBILE001")]
    public static string kCTSMSCellBroadcastConfigChangedNotification => "kCTSMSCellBroadcastConfigChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string kCalBirthdayDefaultAlarmChangedNote => "kCalBirthdayDefaultAlarmChangedNote";
    [Experimental("NETIMOBILE001")]
    public static string kCalEventOccurrenceCacheChangedNotification => "kCalEventOccurrenceCacheChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string kFZACAppBundleIdentifierLaunchNotification => "kFZACAppBundleIdentifierLaunchNotification";
    [Experimental("NETIMOBILE001")]
    public static string kFZVCAppBundleIdentifierLaunchNotification => "kFZVCAppBundleIdentifierLaunchNotification";
    [Experimental("NETIMOBILE001")]
    public static string kFaceTimeChangedNotification => "kFaceTimeChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string kKeepAppsUpToDateEnabledChangedNotification => "kKeepAppsUpToDateEnabledChangedNotification";
    [Experimental("NETIMOBILE001")]
    public static string kVMVoicemailTranscriptionTaskTranscribeAllVoicemails => "kVMVoicemailTranscriptionTaskTranscribeAllVoicemails";
    [Experimental("NETIMOBILE001")]
    public static string developer_mode_status_changed => "security.mac.amfi.developer_mode_status.changed";
    #endregion
}
