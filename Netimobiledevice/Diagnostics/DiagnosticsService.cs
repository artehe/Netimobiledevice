﻿using Microsoft.Extensions.Logging;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Netimobiledevice.Diagnostics;

/// <summary>
/// Provides a service to query MobileGestalt & IORegistry keys, as well functionality to
/// reboot, shutdown, or put the device into sleep mode.
/// </summary>
public sealed class DiagnosticsService(LockdownServiceProvider lockdown, ILogger? logger = null) : LockdownService(lockdown, ServiceNameUsed, GetDiagnosticsServiceConnection(lockdown), logger: logger)
{
    private const string LOCKDOWN_SERVICE_NAME_NEW = "com.apple.mobile.diagnostics_relay";
    private const string LOCKDOWN_SERVICE_NAME_OLD = "com.apple.iosdiagnostics.relay";
    private const string RSD_SERVICE_NAME = "com.apple.mobile.diagnostics_relay.shim.remote";

    private static readonly string[] _mobileGestaltKeys = [
        "3GProximityCapability",
        "3GVeniceCapability",
        "3Gvenice",
        "3d-imagery",
        "3d-maps",
        "64-bit",
        "720p",
        "720pPlaybackCapability",
        "APNCapability",
        "ARM64ExecutionCapability",
        "ARMV6ExecutionCapability",
        "ARMV7ExecutionCapability",
        "ARMV7SExecutionCapability",
        "ASTC",
        "AWDID",
        "AWDLCapability",
        "AccelerometerCapability",
        "AccessibilityCapability",
        "AcousticID",
        "ActivationProtocol",
        "ActiveWirelessTechnology",
        "ActuatorResonantFrequency",
        "AdditionalTextTonesCapability",
        "AggregateDevicePhotoZoomFactor",
        "AggregateDeviceVideoZoomFactor",
        "AirDropCapability",
        "AirDropRestriction",
        "AirplaneMode",
        "AirplayMirroringCapability",
        "AllDeviceCapabilities",
        "Allow32BitApps",
        "AllowOnlyATVCPSDKApps",
        "AllowYouTube",
        "AllowYouTubePlugin",
        "AmbientLightSensorCapability",
        "AmbientLightSensorSerialNumber",
        "ApNonce",
        "ApNonceRetrieve",
        "AppCapacityTVOS",
        "AppStore",
        "AppStoreCapability",
        "AppleInternalInstallCapability",
        "AppleNeuralEngineSubtype",
        "ApplicationInstallationCapability",
        "ArcModuleSerialNumber",
        "ArrowChipID",
        "ArrowUniqueChipID",
        "ArtworkTraits",
        "AssistantCapability",
        "AudioPlaybackCapability",
        "AutoFocusCameraCapability",
        "AvailableDisplayZoomSizes",
        "BacklightCapability",
        "BasebandAPTimeSync",
        "BasebandBoardSnum",
        "BasebandCertId",
        "BasebandChipId",
        "BasebandChipset",
        "BasebandClass",
        "BasebandFirmwareManifestData",
        "BasebandFirmwareUpdateInfo",
        "BasebandFirmwareVersion",
        "BasebandKeyHashInformation",
        "BasebandPostponementStatus",
        "BasebandPostponementStatusBlob",
        "BasebandRegionSKU",
        "BasebandRegionSKURadioTechnology",
        "BasebandSecurityInfoBlob",
        "BasebandSerialNumber",
        "BasebandSkeyId",
        "BasebandStatus",
        "BasebandUniqueId",
        "BatteryCurrentCapacity",
        "BatteryIsCharging",
        "BatteryIsFullyCharged",
        "BatterySerialNumber",
        "BlueLightReductionSupported",
        "BluetoothAddress",
        "BluetoothAddressData",
        "BluetoothCapability",
        "BluetoothLE2Capability",
        "BluetoothLECapability",
        "BoardId",
        "BoardRevision",
        "BootManifestHash",
        "BootNonce",
        "BridgeBuild",
        "BridgeRestoreVersion",
        "BuddyLanguagesAnimationRequiresOptimization",
        "BuildID",
        "BuildVersion",
        "C2KDeviceCapability",
        "CPUArchitecture",
        "CPUSubType",
        "CPUType",
        "CallForwardingCapability",
        "CallWaitingCapability",
        "CallerIDCapability",
        "CameraAppUIVersion",
        "CameraCapability",
        "CameraFlashCapability",
        "CameraFrontFlashCapability",
        "CameraHDR2Capability",
        "CameraHDRVersion",
        "CameraLiveEffectsCapability",
        "CameraMaxBurstLength",
        "CameraRestriction",
        "CarrierBundleInfoArray",
        "CarrierInstallCapability",
        "CellBroadcastCapability",
        "CellularDataCapability",
        "CellularTelephonyCapability",
        "CertificateProductionStatus",
        "CertificateSecurityMode",
        "ChipID",
        "CloudPhotoLibraryCapability",
        "CoastlineGlowRenderingCapability",
        "CompassCalibration",
        "CompassCalibrationDictionary",
        "CompassType",
        "ComputerName",
        "ConferenceCallType",
        "ConfigNumber",
        "ContainsCellularRadioCapability",
        "ContinuityCapability",
        "CoreRoutineCapability",
        "CoverglassSerialNumber",
        "DMin",
        "DataPlanCapability",
        "DebugBoardRevision",
        "DelaySleepForHeadsetClickCapability",
        "DesenseBuild",
        "DeviceAlwaysPrewarmActuator",
        "DeviceBackGlassMaterial",
        "DeviceBackingColor",
        "DeviceBrand",
        "DeviceClass",
        "DeviceClassNumber",
        "DeviceColor",
        "DeviceColorMapPolicy",
        "DeviceCornerRadius",
        "DeviceCoverGlassColor",
        "DeviceCoverGlassMaterial",
        "DeviceCoverMaterial",
        "DeviceEnclosureColor",
        "DeviceEnclosureMaterial",
        "DeviceEnclosureRGBColor",
        "DeviceHasAggregateCamera",
        "DeviceHousingColor",
        "DeviceIsMuseCapable",
        "DeviceKeyboardCalibration",
        "DeviceLaunchTimeLimitScale",
        "DeviceName",
        "DeviceNameString",
        "DevicePrefers3DBuildingStrokes",
        "DevicePrefersBuildingStrokes",
        "DevicePrefersCheapTrafficShaders",
        "DevicePrefersProceduralAntiAliasing",
        "DevicePrefersTrafficAlpha",
        "DeviceProximityCapability",
        "DeviceRGBColor",
        "DeviceRequiresPetalOptimization",
        "DeviceRequiresProximityAmeliorations",
        "DeviceRequiresSoftwareBrightnessCalculations",
        "DeviceSceneUpdateTimeLimitScale",
        "DeviceSubBrand",
        "DeviceSupports1080p",
        "DeviceSupports3DImagery",
        "DeviceSupports3DMaps",
        "DeviceSupports3rdPartyHaptics",
        "DeviceSupports4G",
        "DeviceSupports4k",
        "DeviceSupports64Bit",
        "DeviceSupports720p",
        "DeviceSupports9Pin",
        "DeviceSupportsAOP",
        "DeviceSupportsARKit",
        "DeviceSupportsASTC",
        "DeviceSupportsAdaptiveMapsUI",
        "DeviceSupportsAlwaysListening",
        "DeviceSupportsAlwaysOnCompass",
        "DeviceSupportsAlwaysOnTime",
        "DeviceSupportsApplePencil",
        "DeviceSupportsAutoLowLightVideo",
        "DeviceSupportsAvatars",
        "DeviceSupportsBatteryModuleAuthentication",
        "DeviceSupportsBerkelium2",
        "DeviceSupportsCCK",
        "DeviceSupportsCameraCaptureOnTouchDown",
        "DeviceSupportsCameraDeferredProcessing",
        "DeviceSupportsCameraHaptics",
        "DeviceSupportsCarIntegration",
        "DeviceSupportsCinnamon",
        "DeviceSupportsClosedLoopHaptics",
        "DeviceSupportsCrudeProx",
        "DeviceSupportsDClr",
        "DeviceSupportsDoNotDisturbWhileDriving",
        "DeviceSupportsELabel",
        "DeviceSupportsEnhancedAC3",
        "DeviceSupportsEnvironmentalDosimetry",
        "DeviceSupportsExternalHDR",
        "DeviceSupportsFloorCounting",
        "DeviceSupportsHDRDeferredProcessing",
        "DeviceSupportsHMEInARKit",
        "DeviceSupportsHaptics",
        "DeviceSupportsHardwareDetents",
        "DeviceSupportsHeartHealthAlerts",
        "DeviceSupportsHeartRateVariability",
        "DeviceSupportsHiResBuildings",
        "DeviceSupportsLineIn",
        "DeviceSupportsLiquidDetection_CorrosionMitigation",
        "DeviceSupportsLivePhotoAuto",
        "DeviceSupportsLongFormAudio",
        "DeviceSupportsMapsBlurredUI",
        "DeviceSupportsMapsOpticalHeading",
        "DeviceSupportsMomentCapture",
        "DeviceSupportsNFC",
        "DeviceSupportsNavigation",
        "DeviceSupportsNewton",
        "DeviceSupportsOnDemandPhotoAnalysis",
        "DeviceSupportsP3ColorspaceVideoRecording",
        "DeviceSupportsPeriodicALSUpdates",
        "DeviceSupportsPhotosLocalLight",
        "DeviceSupportsPortraitIntensityAdjustments",
        "DeviceSupportsPortraitLightEffectFilters",
        "DeviceSupportsRGB10",
        "DeviceSupportsRaiseToSpeak",
        "DeviceSupportsSiDP",
        "DeviceSupportsSideButtonClickSpeed",
        "DeviceSupportsSimplisticRoadMesh",
        "DeviceSupportsSingleCameraPortrait",
        "DeviceSupportsSiriBargeIn",
        "DeviceSupportsSiriSpeaks",
        "DeviceSupportsSiriSpokenMessages",
        "DeviceSupportsSpatialOverCapture",
        "DeviceSupportsStereoAudioRecording",
        "DeviceSupportsStudioLightPortraitPreview",
        "DeviceSupportsSwimmingWorkouts",
        "DeviceSupportsTapToWake",
        "DeviceSupportsTelephonyOverUSB",
        "DeviceSupportsTethering",
        "DeviceSupportsToneMapping",
        "DeviceSupportsUSBTypeC",
        "DeviceSupportsVSHCompensation",
        "DeviceSupportsVoiceOverCanUseSiriVoice",
        "DeviceSupportsWebkit",
        "DeviceSupportsWirelessSplitting",
        "DeviceSupportsYCbCr10",
        "DeviceVariant",
        "DeviceVariantGuess",
        "DiagData",
        "DictationCapability",
        "DieId",
        "DiskUsage",
        "DisplayDriverICChipID",
        "DisplayFCCLogosViaSoftwareCapability",
        "DisplayMirroringCapability",
        "DisplayPortCapability",
        "DualSIMActivationPolicyCapable",
        "EUICCChipID",
        "EffectiveProductionStatus",
        "EffectiveProductionStatusAp",
        "EffectiveProductionStatusSEP",
        "EffectiveSecurityMode",
        "EffectiveSecurityModeAp",
        "EffectiveSecurityModeSEP",
        "EncodeAACCapability",
        "EncryptedDataPartitionCapability",
        "EnforceCameraShutterClick",
        "EnforceGoogleMail",
        "EthernetMacAddress",
        "EthernetMacAddressData",
        "ExplicitContentRestriction",
        "ExternalChargeCapability",
        "ExternalPowerSourceConnected",
        "FDRSealingStatus",
        "FMFAllowed",
        "FaceTimeBackCameraTemporalNoiseReductionMode",
        "FaceTimeBitRate2G",
        "FaceTimeBitRate3G",
        "FaceTimeBitRateLTE",
        "FaceTimeBitRateWiFi",
        "FaceTimeCameraRequiresFastSwitchOptions",
        "FaceTimeCameraSupportsHardwareFaceDetection",
        "FaceTimeDecodings",
        "FaceTimeEncodings",
        "FaceTimeFrontCameraTemporalNoiseReductionMode",
        "FaceTimePhotosOptIn",
        "FaceTimePreferredDecoding",
        "FaceTimePreferredEncoding",
        "FirmwareNonce",
        "FirmwarePreflightInfo",
        "FirmwareVersion",
        "FirstPartyLaunchTimeLimitScale",
        "ForwardCameraCapability",
        "FrontCameraOffsetFromDisplayCenter",
        "FrontCameraRotationFromDisplayNormal",
        "FrontFacingCameraAutoHDRCapability",
        "FrontFacingCameraBurstCapability",
        "FrontFacingCameraCapability",
        "FrontFacingCameraHDRCapability",
        "FrontFacingCameraHDROnCapability",
        "FrontFacingCameraHFRCapability",
        "FrontFacingCameraHFRVideoCapture1080pMaxFPS",
        "FrontFacingCameraHFRVideoCapture720pMaxFPS",
        "FrontFacingCameraMaxVideoZoomFactor",
        "FrontFacingCameraModuleSerialNumber",
        "FrontFacingCameraStillDurationForBurst",
        "FrontFacingCameraVideoCapture1080pMaxFPS",
        "FrontFacingCameraVideoCapture4kMaxFPS",
        "FrontFacingCameraVideoCapture720pMaxFPS",
        "FrontFacingIRCameraModuleSerialNumber",
        "FrontFacingIRStructuredLightProjectorModuleSerialNumber",
        "Full6FeaturesCapability",
        "GPSCapability",
        "GSDeviceName",
        "GameKitCapability",
        "GasGaugeBatteryCapability",
        "GreenTeaDeviceCapability",
        "GyroscopeCapability",
        "H264EncoderCapability",
        "HDRImageCaptureCapability",
        "HDVideoCaptureCapability",
        "HEVCDecoder10bitSupported",
        "HEVCDecoder12bitSupported",
        "HEVCDecoder8bitSupported",
        "HEVCEncodingCapability",
        "HMERefreshRateInARKit",
        "HWModelStr",
        "HallEffectSensorCapability",
        "HardwareEncodeSnapshotsCapability",
        "HardwareKeyboardCapability",
        "HardwarePlatform",
        "HardwareSnapshotsRequirePurpleGfxCapability",
        "HasAllFeaturesCapability",
        "HasAppleNeuralEngine",
        "HasBaseband",
        "HasBattery",
        "HasDaliMode",
        "HasExtendedColorDisplay",
        "HasIcefall",
        "HasInternalSettingsBundle",
        "HasMesa",
        "HasPKA",
        "HasSEP",
        "HasSpringBoard",
        "HasThinBezel",
        "HealthKitCapability",
        "HearingAidAudioEqualizationCapability",
        "HearingAidLowEnergyAudioCapability",
        "HearingAidPowerReductionCapability",
        "HiDPICapability",
        "HiccoughInterval",
        "HideNonDefaultApplicationsCapability",
        "HighestSupportedVideoMode",
        "HomeButtonType",
        "HomeScreenWallpaperCapability",
        "IDAMCapability",
        "IOSurfaceBackedImagesCapability",
        "IOSurfaceFormatDictionary",
        "IceFallID",
        "IcefallInRestrictedMode",
        "IcefallInfo",
        "Image4CryptoHashMethod",
        "Image4Supported",
        "InDiagnosticsMode",
        "IntegratedCircuitCardIdentifier",
        "IntegratedCircuitCardIdentifier2",
        "InternalBuild",
        "InternationalMobileEquipmentIdentity",
        "InternationalMobileEquipmentIdentity2",
        "InternationalSettingsCapability",
        "InverseDeviceID",
        "IsEmulatedDevice",
        "IsLargeFormatPhone",
        "IsPwrOpposedVol",
        "IsServicePart",
        "IsSimulator",
        "IsThereEnoughBatteryLevelForSoftwareUpdate",
        "IsUIBuild",
        "JasperSerialNumber",
        "LTEDeviceCapability",
        "LaunchTimeLimitScaleSupported",
        "LisaCapability",
        "LoadThumbnailsWhileScrollingCapability",
        "LocalizedDeviceNameString",
        "LocationRemindersCapability",
        "LocationServicesCapability",
        "LowPowerWalletMode",
        "LunaFlexSerialNumber",
        "LynxPublicKey",
        "LynxSerialNumber",
        "MLBSerialNumber",
        "MLEHW",
        "MMSCapability",
        "MacBridgingKeys",
        "MagnetometerCapability",
        "MainDisplayRotation",
        "MainScreenCanvasSizes",
        "MainScreenClass",
        "MainScreenHeight",
        "MainScreenOrientation",
        "MainScreenPitch",
        "MainScreenScale",
        "MainScreenStaticInfo",
        "MainScreenWidth",
        "MarketingNameString",
        "MarketingProductName",
        "MarketingVersion",
        "MaxH264PlaybackLevel",
        "MaximumScreenScale",
        "MedusaFloatingLiveAppCapability",
        "MedusaOverlayAppCapability",
        "MedusaPIPCapability",
        "MedusaPinnedAppCapability",
        "MesaSerialNumber",
        "MetalCapability",
        "MicrophoneCapability",
        "MicrophoneCount",
        "MinimumSupportediTunesVersion",
        "MixAndMatchPrevention",
        "MobileDeviceMinimumVersion",
        "MobileEquipmentIdentifier",
        "MobileEquipmentInfoBaseId",
        "MobileEquipmentInfoBaseProfile",
        "MobileEquipmentInfoBaseVersion",
        "MobileEquipmentInfoCSN",
        "MobileEquipmentInfoDisplayCSN",
        "MobileSubscriberCountryCode",
        "MobileSubscriberNetworkCode",
        "MobileWifi",
        "ModelNumber",
        "MonarchLowEndHardware",
        "MultiLynxPublicKeyArray",
        "MultiLynxSerialNumberArray",
        "MultitaskingCapability",
        "MultitaskingGesturesCapability",
        "MusicStore",
        "MusicStoreCapability",
        "N78aHack",
        "NFCRadio",
        "NFCRadioCalibrationDataPresent",
        "NFCUniqueChipID",
        "NVRAMDictionary",
        "NandControllerUID",
        "NavajoFusingState",
        "NikeIpodCapability",
        "NotGreenTeaDeviceCapability",
        "OLEDDisplay",
        "OTAActivationCapability",
        "OfflineDictationCapability",
        "OpenGLES1Capability",
        "OpenGLES2Capability",
        "OpenGLES3Capability",
        "OpenGLESVersion",
        "PTPLargeFilesCapability",
        "PanelSerialNumber",
        "PanoramaCameraCapability",
        "PartitionType",
        "PasswordConfigured",
        "PasswordProtected",
        "PearlCameraCapability",
        "PearlIDCapability",
        "PeekUICapability",
        "PeekUIWidth",
        "Peer2PeerCapability",
        "PersonalHotspotCapability",
        "PhoneNumber",
        "PhoneNumber2",
        "PhosphorusCapability",
        "PhotoAdjustmentsCapability",
        "PhotoCapability",
        "PhotoSharingCapability",
        "PhotoStreamCapability",
        "PhotosPostEffectsCapability",
        "PiezoClickerCapability",
        "PintoMacAddress",
        "PintoMacAddressData",
        "PipelinedStillImageProcessingCapability",
        "PlatformStandAloneContactsCapability",
        "PlatinumCapability",
        "ProductHash",
        "ProductName",
        "ProductType",
        "ProductVersion",
        "ProximitySensorCalibration",
        "ProximitySensorCalibrationDictionary",
        "ProximitySensorCapability",
        "RF-exposure-separation-distance",
        "RFExposureSeparationDistance",
        "RawPanelSerialNumber",
        "RearCameraCapability",
        "RearCameraOffsetFromDisplayCenter",
        "RearFacingCamera60fpsVideoCaptureCapability",
        "RearFacingCameraAutoHDRCapability",
        "RearFacingCameraBurstCapability",
        "RearFacingCameraCapability",
        "RearFacingCameraHDRCapability",
        "RearFacingCameraHDROnCapability",
        "RearFacingCameraHFRCapability",
        "RearFacingCameraHFRVideoCapture1080pMaxFPS",
        "RearFacingCameraHFRVideoCapture720pMaxFPS",
        "RearFacingCameraMaxVideoZoomFactor",
        "RearFacingCameraModuleSerialNumber",
        "RearFacingCameraStillDurationForBurst",
        "RearFacingCameraSuperWideCameraCapability",
        "RearFacingCameraTimeOfFlightCameraCapability",
        "RearFacingCameraVideoCapture1080pMaxFPS",
        "RearFacingCameraVideoCapture4kMaxFPS",
        "RearFacingCameraVideoCapture720pMaxFPS",
        "RearFacingCameraVideoCaptureFPS",
        "RearFacingLowLightCameraCapability",
        "RearFacingSuperWideCameraModuleSerialNumber",
        "RearFacingTelephotoCameraCapability",
        "RearFacingTelephotoCameraModuleSerialNumber",
        "RecoveryOSVersion",
        "RegionCode",
        "RegionInfo",
        "RegionSupportsCinnamon",
        "RegionalBehaviorAll",
        "RegionalBehaviorChinaBrick",
        "RegionalBehaviorEUVolumeLimit",
        "RegionalBehaviorGB18030",
        "RegionalBehaviorGoogleMail",
        "RegionalBehaviorNTSC",
        "RegionalBehaviorNoPasscodeLocationTiles",
        "RegionalBehaviorNoVOIP",
        "RegionalBehaviorNoWiFi",
        "RegionalBehaviorShutterClick",
        "RegionalBehaviorValid",
        "RegionalBehaviorVolumeLimit",
        "RegulatoryModelNumber",
        "ReleaseType",
        "RemoteBluetoothAddress",
        "RemoteBluetoothAddressData",
        "RenderWideGamutImagesAtDisplayTime",
        "RendersLetterPressSlowly",
        "RequiredBatteryLevelForSoftwareUpdate",
        "RestoreOSBuild",
        "RestrictedCountryCodes",
        "RingerSwitchCapability",
        "RosalineSerialNumber",
        "RoswellChipID",
        "RotateToWakeStatus",
        "SBAllowSensitiveUI",
        "SBCanForceDebuggingInfo",
        "SDIOManufacturerTuple",
        "SDIOProductInfo",
        "SEInfo",
        "SEPNonce",
        "SIMCapability",
        "SIMPhonebookCapability",
        "SIMStatus",
        "SIMStatus2",
        "SIMTrayStatus",
        "SIMTrayStatus2",
        "SMSCapability",
        "SavageChipID",
        "SavageInfo",
        "SavageSerialNumber",
        "SavageUID",
        "ScreenDimensions",
        "ScreenDimensionsCapability",
        "ScreenRecorderCapability",
        "ScreenSerialNumber",
        "SecondaryBluetoothMacAddress",
        "SecondaryBluetoothMacAddressData",
        "SecondaryEthernetMacAddress",
        "SecondaryEthernetMacAddressData",
        "SecondaryWifiMacAddress",
        "SecondaryWifiMacAddressData",
        "SecureElement",
        "SecureElementID",
        "SecurityDomain",
        "SensitiveUICapability",
        "SerialNumber",
        "ShoeboxCapability",
        "ShouldHactivate",
        "SiKACapability",
        "SigningFuse",
        "SiliconBringupBoard",
        "SimultaneousCallAndDataCurrentlySupported",
        "SimultaneousCallAndDataSupported",
        "SiriGestureCapability",
        "SiriOfflineCapability",
        "Skey",
        "SoftwareBehavior",
        "SoftwareBundleVersion",
        "SoftwareDimmingAlpha",
        "SpeakerCalibrationMiGa",
        "SpeakerCalibrationSpGa",
        "SpeakerCalibrationSpTS",
        "SphereCapability",
        "StarkCapability",
        "StockholmJcopInfo",
        "StrictWakeKeyboardCases",
        "SupportedDeviceFamilies",
        "SupportedKeyboards",
        "SupportsBurninMitigation",
        "SupportsEDUMU",
        "SupportsForceTouch",
        "SupportsIrisCapture",
        "SupportsLowPowerMode",
        "SupportsPerseus",
        "SupportsRotateToWake",
        "SupportsSOS",
        "SupportsSSHBButtonType",
        "SupportsTouchRemote",
        "SysCfg",
        "SysCfgDict",
        "SystemImageID",
        "SystemTelephonyOfAnyKindCapability",
        "TVOutCrossfadeCapability",
        "TVOutSettingsCapability",
        "TelephonyCapability",
        "TelephonyMaximumGeneration",
        "TimeSyncCapability",
        "TopModuleAuthChipID",
        "TouchDelivery120Hz",
        "TouchIDCapability",
        "TristarID",
        "UIBackgroundQuality",
        "UIParallaxCapability",
        "UIProceduralWallpaperCapability",
        "UIReachability",
        "UMTSDeviceCapability",
        "UnifiedIPodCapability",
        "UniqueChipID",
        "UniqueDeviceID",
        "UniqueDeviceIDData",
        "UserAssignedDeviceName",
        "UserIntentPhysicalButtonCGRect",
        "UserIntentPhysicalButtonCGRectString",
        "UserIntentPhysicalButtonNormalizedCGRect",
        "VOIPCapability",
        "VeniceCapability",
        "VibratorCapability",
        "VideoCameraCapability",
        "VideoStillsCapability",
        "VoiceControlCapability",
        "VolumeButtonCapability",
        "WAGraphicQuality",
        "WAPICapability",
        "WLANBkgScanCache",
        "WSKU",
        "WatchCompanionCapability",
        "WatchSupportsAutoPlaylistPlayback",
        "WatchSupportsHighQualityClockFaceGraphics",
        "WatchSupportsListeningOnGesture",
        "WatchSupportsMusicStreaming",
        "WatchSupportsSiriCommute",
        "WiFiCallingCapability",
        "WiFiCapability",
        "WifiAddress",
        "WifiAddressData",
        "WifiAntennaSKUVersion",
        "WifiCallingSecondaryDeviceCapability",
        "WifiChipset",
        "WifiFirmwareVersion",
        "WifiVendor",
        "WirelessBoardSnum",
        "WirelessChargingCapability",
        "YonkersChipID",
        "YonkersSerialNumber",
        "YonkersUID",
        "YouTubeCapability",
        "YouTubePluginCapability",
        "accelerometer",
        "accessibility",
        "additional-text-tones",
        "aggregate-cam-photo-zoom",
        "aggregate-cam-video-zoom",
        "airDropRestriction",
        "airplay-mirroring",
        "airplay-no-mirroring",
        "all-features",
        "allow-32bit-apps",
        "ambient-light-sensor",
        "ane",
        "any-telephony",
        "apn",
        "apple-internal-install",
        "applicationInstallation",
        "arkit",
        "arm64",
        "armv6",
        "armv7",
        "armv7s",
        "assistant",
        "auto-focus",
        "auto-focus-camera",
        "baseband-chipset",
        "bitrate-2g",
        "bitrate-3g",
        "bitrate-lte",
        "bitrate-wifi",
        "bluetooth",
        "bluetooth-le",
        "board-id",
        "boot-manifest-hash",
        "boot-nonce",
        "builtin-mics",
        "c2k-device",
        "calibration",
        "call-forwarding",
        "call-waiting",
        "caller-id",
        "camera-flash",
        "camera-front",
        "camera-front-flash",
        "camera-rear",
        "cameraRestriction",
        "car-integration",
        "cell-broadcast",
        "cellular-data",
        "certificate-production-status",
        "certificate-security-mode",
        "chip-id",
        "class",
        "closed-loop",
        "config-number",
        "contains-cellular-radio",
        "crypto-hash-method",
        "dali-mode",
        "data-plan",
        "debug-board-revision",
        "delay-sleep-for-headset-click",
        "device-color-policy",
        "device-colors",
        "device-name",
        "device-name-localized",
        "dictation",
        "die-id",
        "display-mirroring",
        "display-rotation",
        "displayport",
        "does-not-support-gamekit",
        "effective-production-status",
        "effective-production-status-ap",
        "effective-production-status-sep",
        "effective-security-mode",
        "effective-security-mode-ap",
        "effective-security-mode-sep",
        "enc-top-type",
        "encode-aac",
        "encrypted-data-partition",
        "enforce-googlemail",
        "enforce-shutter-click",
        "euicc-chip-id",
        "explicitContentRestriction",
        "face-detection-support",
        "fast-switch-options",
        "fcc-logos-via-software",
        "fcm-type",
        "firmware-version",
        "flash",
        "front-auto-hdr",
        "front-burst",
        "front-burst-image-duration",
        "front-facing-camera",
        "front-flash-capability",
        "front-hdr",
        "front-hdr-on",
        "front-max-video-fps-1080p",
        "front-max-video-fps-4k",
        "front-max-video-fps-720p",
        "front-max-video-zoom",
        "front-slowmo",
        "full-6",
        "function-button_halleffect",
        "function-button_ringerab",
        "gamekit",
        "gas-gauge-battery",
        "gps",
        "gps-capable",
        "green-tea",
        "gyroscope",
        "h264-encoder",
        "hall-effect-sensor",
        "haptics",
        "hardware-keyboard",
        "has-sphere",
        "hd-video-capture",
        "hdr-image-capture",
        "healthkit",
        "hearingaid-audio-equalization",
        "hearingaid-low-energy-audio",
        "hearingaid-power-reduction",
        "hiccough-interval",
        "hide-non-default-apps",
        "hidpi",
        "home-button-type",
        "homescreen-wallpaper",
        "hw-encode-snapshots",
        "hw-snapshots-need-purplegfx",
        "iAP2Capability",
        "iPadCapability",
        "iTunesFamilyID",
        "iap2-protocol-supported",
        "image4-supported",
        "international-settings",
        "io-surface-backed-images",
        "ipad",
        "kConferenceCallType",
        "kSimultaneousCallAndDataCurrentlySupported",
        "kSimultaneousCallAndDataSupported",
        "large-format-phone",
        "live-effects",
        "live-photo-capture",
        "load-thumbnails-while-scrolling",
        "location-reminders",
        "location-services",
        "low-power-wallet-mode",
        "lte-device",
        "magnetometer",
        "main-screen-class",
        "main-screen-height",
        "main-screen-orientation",
        "main-screen-pitch",
        "main-screen-scale",
        "main-screen-width",
        "marketing-name",
        "mesa",
        "metal",
        "microphone",
        "mix-n-match-prevention-status",
        "mms",
        "modelIdentifier",
        "multi-touch",
        "multitasking",
        "multitasking-gesture",
        "n78a-mode",
        "name",
        "navigation",
        "nfc",
        "nfcWithRadio",
        "nike-ipod",
        "nike-support",
        "no-coreroutine",
        "no-hi-res-buildings",
        "no-simplistic-road-mesh",
        "not-green-tea",
        "offline-dictation",
        "opal",
        "opengles-1",
        "opengles-2",
        "opengles-3",
        "opposed-power-vol-buttons",
        "ota-activation",
        "panorama",
        "peek-ui-width",
        "peer-peer",
        "personal-hotspot",
        "photo-adjustments",
        "photo-stream",
        "piezo-clicker",
        "pipelined-stillimage-capability",
        "platinum",
        "post-effects",
        "pressure",
        "prox-sensor",
        "proximity-sensor",
        "ptp-large-files",
        "public-key-accelerator",
        "rear-auto-hdr",
        "rear-burst",
        "rear-burst-image-duration",
        "rear-cam-telephoto-capability",
        "rear-facing-camera",
        "rear-hdr",
        "rear-hdr-on",
        "rear-max-slomo-video-fps-1080p",
        "rear-max-slomo-video-fps-720p",
        "rear-max-video-fps-1080p",
        "rear-max-video-fps-4k",
        "rear-max-video-fps-720p",
        "rear-max-video-frame_rate",
        "rear-max-video-zoom",
        "rear-slowmo",
        "regulatory-model-number",
        "ringer-switch",
        "role",
        "s8000\")",
        "s8003\")",
        "sandman-support",
        "screen-dimensions",
        "sensitive-ui",
        "shoebox",
        "sika-support",
        "sim",
        "sim-phonebook",
        "siri-gesture",
        "slow-letterpress-rendering",
        "sms",
        "software-bundle-version",
        "software-dimming-alpha",
        "stand-alone-contacts",
        "still-camera",
        "stockholm",
        "supports-always-listening",
        "t7000\")",
        "telephony",
        "telephony-maximum-generation",
        "thin-bezel",
        "tnr-mode-back",
        "tnr-mode-front",
        "touch-id",
        "tv-out-crossfade",
        "tv-out-settings",
        "ui-background-quality",
        "ui-no-parallax",
        "ui-no-procedural-wallpaper",
        "ui-pip",
        "ui-reachability",
        "ui-traffic-cheap-shaders",
        "ui-weather-quality",
        "unified-ipod",
        "umts-device",
        "unique-chip-id",
        "venice",
        "video-camera",
        "video-cap",
        "video-stills",
        "voice-control",
        "voip",
        "volume-buttons",
        "wapi",
        "watch-companion",
        "wi-fi",
        "wifi",
        "wifi-antenna-sku-info",
        "wifi-chipset",
        "wifi-module-sn",
        "wlan",
        "wlan.background-scan-cache",
        "youtube",
        "youtubePlugin"
    ];

    private static string ServiceNameUsed { get; set; } = LOCKDOWN_SERVICE_NAME_NEW;

    private static ServiceConnection? GetDiagnosticsServiceConnection(LockdownServiceProvider lockdown)
    {
        ServiceConnection? service = null;
        if (lockdown is LockdownClient) {
            try {
                service = lockdown.StartLockdownService(LOCKDOWN_SERVICE_NAME_NEW);
                ServiceNameUsed = LOCKDOWN_SERVICE_NAME_NEW;
            }
            catch (Exception ex) {
                lockdown.Logger.LogWarning(ex, "Failed to start the new lockdown service, falling back to the old service.");
                service = lockdown.StartLockdownService(LOCKDOWN_SERVICE_NAME_OLD);
                ServiceNameUsed = LOCKDOWN_SERVICE_NAME_OLD;
            }
        }
        else {
            ServiceNameUsed = RSD_SERVICE_NAME;
        }
        return service;
    }

    private PropertyNode ExecuteCommand(StringNode action)
    {
        DictionaryNode command = new DictionaryNode() {
            { "Request", action }
        };

        DictionaryNode response = Service.SendReceivePlist(command)?.AsDictionaryNode() ?? [];
        if (response.TryGetValue("Status", out PropertyNode? statusNode) && statusNode.AsStringNode().Value != "Success") {
            throw new DiagnosticsException($"Failed to perform action: {action.Value}");
        }
        return response["Diagnostics"];
    }

    private async Task<PropertyNode> ExecuteCommandAsync(StringNode action, CancellationToken cancellationToken)
    {
        DictionaryNode command = new DictionaryNode() {
            { "Request", action }
        };

        PropertyNode? response = await Service.SendReceivePlistAsync(command, cancellationToken).ConfigureAwait(false);
        DictionaryNode dict = response?.AsDictionaryNode() ?? [];
        if (dict.TryGetValue("Status", out PropertyNode? statusNode) && statusNode.AsStringNode().Value != "Success") {
            throw new DiagnosticsException($"Failed to perform action: {action.Value}");
        }
        return dict["Diagnostics"];
    }

    public DictionaryNode IORegistry(string? plane = null, string? name = null, string? ioClass = null)
    {
        DictionaryNode dict = new DictionaryNode() {
            { "Request", new StringNode("IORegistry") }
        };

        if (!string.IsNullOrWhiteSpace(plane)) {
            dict.Add("CurrentPlane", new StringNode(plane));
        }
        if (!string.IsNullOrWhiteSpace(name)) {
            dict.Add("EntryName", new StringNode(name));
        }
        if (!string.IsNullOrWhiteSpace(ioClass)) {
            dict.Add("EntryClass", new StringNode(ioClass));
        }

        DictionaryNode response = Service.SendReceivePlist(dict)?.AsDictionaryNode() ?? [];
        if (response.ContainsKey("Status") && response["Status"].AsStringNode().Value != "Success") {
            throw new DiagnosticsException($"Got invalid response: {response}");
        }

        if (response.ContainsKey("Diagnostics")) {
            DictionaryNode diagnosticsDict = response["Diagnostics"].AsDictionaryNode();
            return diagnosticsDict["IORegistry"].AsDictionaryNode();
        }
        return [];
    }

    public async Task<DictionaryNode> IORegistryAsync(string? plane = null, string? name = null, string? ioClass = null, CancellationToken cancellationToken = default)
    {
        DictionaryNode dict = new DictionaryNode() {
            { "Request", new StringNode("IORegistry") }
        };

        if (!string.IsNullOrWhiteSpace(plane)) {
            dict.Add("CurrentPlane", new StringNode(plane));
        }
        if (!string.IsNullOrWhiteSpace(name)) {
            dict.Add("EntryName", new StringNode(name));
        }
        if (!string.IsNullOrWhiteSpace(ioClass)) {
            dict.Add("EntryClass", new StringNode(ioClass));
        }

        PropertyNode? response = await Service.SendReceivePlistAsync(dict, cancellationToken).ConfigureAwait(false);
        DictionaryNode responseDict = response?.AsDictionaryNode() ?? [];
        if (responseDict.TryGetValue("Status", out PropertyNode? statusNode) && statusNode.AsStringNode().Value != "Success") {
            throw new DiagnosticsException($"Got invalid response: {response}");
        }

        if (responseDict.TryGetValue("Diagnostics", out PropertyNode? diagnosticsNode)) {
            DictionaryNode diagnosticsDict = diagnosticsNode.AsDictionaryNode();
            return diagnosticsDict["IORegistry"].AsDictionaryNode();
        }
        return [];
    }

    public DictionaryNode GetBattery()
    {
        return IORegistry(null, null, "IOPMPowerSource");
    }

    public async Task<DictionaryNode> GetBatteryAsync(CancellationToken cancellationToken)
    {
        return await IORegistryAsync(null, null, "IOPMPowerSource", cancellationToken).ConfigureAwait(false);
    }

    public Dictionary<string, ulong> GetStorageDetails()
    {
        Dictionary<string, ulong> storageData = [];
        DictionaryNode storageList = MobileGestalt(["DiskUsage"]);
        if (storageList.ContainsKey("DiskUsage")) {
            foreach (KeyValuePair<string, PropertyNode> kvp in storageList["DiskUsage"].AsDictionaryNode()) {
                storageData.Add(kvp.Key, kvp.Value.AsIntegerNode().Value);
            }
        }
        return storageData;
    }

    public async Task<Dictionary<string, ulong>> GetStorageDetailsAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, ulong> storageData = [];
        DictionaryNode storageList = await MobileGestaltAsync(["DiskUsage"], cancellationToken).ConfigureAwait(false);
        if (storageList.ContainsKey("DiskUsage")) {
            foreach (KeyValuePair<string, PropertyNode> kvp in storageList["DiskUsage"].AsDictionaryNode()) {
                storageData.Add(kvp.Key, kvp.Value.AsIntegerNode().Value);
            }
        }
        return storageData;
    }

    public DictionaryNode GetBatteryDetails()
    {
        string[] keys = [
            "HasBattery",
            "IsThereEnoughBatteryLevelForSoftwareUpdate",
            "RequiredBatteryLevelForSoftwareUpdate",
            "BatteryCurrentCapacity",
            "BatteryIsCharging",
            "BatteryIsFullyCharged",
            "BatterySerialNumber"
        ];
        DictionaryNode batteryData = MobileGestalt(keys);
        return batteryData;
    }

    public async Task<DictionaryNode> GetBatteryDetails(CancellationToken cancellationToken)
    {
        string[] keys = [
            "HasBattery",
            "IsThereEnoughBatteryLevelForSoftwareUpdate",
            "RequiredBatteryLevelForSoftwareUpdate",
            "BatteryCurrentCapacity",
            "BatteryIsCharging",
            "BatteryIsFullyCharged",
            "BatterySerialNumber"
        ];
        DictionaryNode batteryData = await MobileGestaltAsync(keys, cancellationToken).ConfigureAwait(false);
        return batteryData;
    }

    public PropertyNode Info(string diagnosticType = "All")
    {
        return ExecuteCommand(new StringNode(diagnosticType));
    }

    public async Task<PropertyNode> Info(string diagnosticType = "All", CancellationToken cancellationToken = default)
    {
        return await ExecuteCommandAsync(new StringNode(diagnosticType), cancellationToken).ConfigureAwait(false);
    }

    public DictionaryNode MobileGestalt(string[] keys)
    {
        DictionaryNode request = new DictionaryNode() {
            { "Request", new StringNode("MobileGestalt") },
        };
        ArrayNode mobileGestaltKeys = [];
        foreach (string key in keys) {
            mobileGestaltKeys.Add(new StringNode(key));
        }
        request.Add("MobileGestaltKeys", mobileGestaltKeys);

        DictionaryNode response = Service.SendReceivePlist(request)?.AsDictionaryNode() ?? [];
        if (response.ContainsKey("Status") && response["Status"].AsStringNode().Value != "Success") {
            throw new DiagnosticsException("Failed to query MobileGestalt");
        }

        if (response.TryGetValue("Diagnostics", out PropertyNode? diagnosticsNode)) {
            if (diagnosticsNode.AsDictionaryNode().TryGetValue("MobileGestalt", out PropertyNode? mobileGestaltNode)) {
                PropertyNode status = mobileGestaltNode.AsDictionaryNode()["Status"];
                if (status.AsStringNode().Value == "MobileGestaltDeprecated") {
                    throw new DeprecatedException("Failed to query MobileGestalt; deprecated as of iOS >= 17.4.");
                }
                else if (status.AsStringNode().Value != "Success") {
                    throw new DiagnosticsException("Failed to query MobileGestalt");
                }
                return mobileGestaltNode.AsDictionaryNode();
            }
        }

        throw new DiagnosticsException("Failed to query MobileGestalt");
    }

    public async Task<DictionaryNode> MobileGestaltAsync(string[] keys, CancellationToken cancellationToken)
    {
        DictionaryNode request = new DictionaryNode() {
            { "Request", new StringNode("MobileGestalt") },
        };
        ArrayNode mobileGestaltKeys = [];
        foreach (string key in keys) {
            mobileGestaltKeys.Add(new StringNode(key));
        }
        request.Add("MobileGestaltKeys", mobileGestaltKeys);

        PropertyNode? response = await Service.SendReceivePlistAsync(request, cancellationToken).ConfigureAwait(false);
        DictionaryNode dict = response?.AsDictionaryNode() ?? [];
        if (dict.TryGetValue("Status", out PropertyNode? statusNode) && statusNode.AsStringNode().Value != "Success") {
            throw new DiagnosticsException("Failed to query MobileGestalt");
        }

        if (dict.TryGetValue("Diagnostics", out PropertyNode? diagnosticsNode)) {
            if (diagnosticsNode.AsDictionaryNode().TryGetValue("MobileGestalt", out PropertyNode? mobileGestaltNode)) {
                PropertyNode status = mobileGestaltNode.AsDictionaryNode()["Status"];
                if (status.AsStringNode().Value == "MobileGestaltDeprecated") {
                    throw new DeprecatedException("Failed to query MobileGestalt; deprecated as of iOS >= 17.4.");
                }
                else if (status.AsStringNode().Value != "Success") {
                    throw new DiagnosticsException("Failed to query MobileGestalt");
                }
                return mobileGestaltNode.AsDictionaryNode();
            }
        }

        throw new DiagnosticsException("Failed to query MobileGestalt");
    }

    /// <summary>
    /// Query MobileGestalt using all the available keys
    /// </summary>
    /// <returns></returns>
    public DictionaryNode MobileGestalt()
    {
        return MobileGestalt([.. _mobileGestaltKeys]);
    }

    /// <summary>
    /// Query MobileGestalt using all the available keys
    /// </summary>
    /// <returns></returns>
    public async Task<DictionaryNode> MobileGestaltAsync(CancellationToken cancellationToken)
    {
        return await MobileGestaltAsync([.. _mobileGestaltKeys], cancellationToken).ConfigureAwait(false);
    }

    public void Restart()
    {
        ExecuteCommand(new StringNode("Restart"));
    }

    public async Task Restart(CancellationToken cancellationToken)
    {
        await ExecuteCommandAsync(new StringNode("Restart"), cancellationToken).ConfigureAwait(false);
    }

    public void Shutdown()
    {
        ExecuteCommand(new StringNode("Shutdown"));
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        await ExecuteCommandAsync(new StringNode("Shutdown"), cancellationToken).ConfigureAwait(false);
    }

    public void Sleep()
    {
        ExecuteCommand(new StringNode("Sleep"));
    }

    public async Task SleepAsync(CancellationToken cancellationToken)
    {
        await ExecuteCommandAsync(new StringNode("Sleep"), cancellationToken).ConfigureAwait(false);
    }
}
