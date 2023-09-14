using Netimobiledevice.Exceptions;

namespace Netimobiledevice.Lockdown
{
    public enum LockdownError
    {
        Success = 0,
        InvalidArg = -1,
        InvalidConf = -2,
        PlistError = -3,
        PairingFailed = -4,
        SslError = -5,
        DictError = -6,
        ReceiveTimeout = -7,
        MuxError = -8,
        NoRunningSession = -9,
        InvalidResponse = -10,
        MissingKey = -11,
        MissingValue = -12,
        GetProhibited = -13,
        SetProhibited = -14,
        RemoveProhibited = -15,
        ImmutableValue = -16,
        PasswordProtected = -17,
        UserDeniedPairing = -18,
        PairingDialogResponsePending = -19,
        MissingHostId = -20,
        InvalidHostID = -21,
        SessionActive = -22,
        SessionInactive = -23,
        MissingSessionId = -24,
        InvalidSessionId = -25,
        MissingService = -26,
        InvalidService = -27,
        ServiceLimit = -28,
        MissingPairRecord = -29,
        SavePairRecordFailed = -30,
        InvalidPairRecord = -31,
        InvalidActivationRecord = -32,
        MissingActivationRecord = -33,
        ServiceProhibited = -34,
        EscrowLocked = -35,
        PairingProhibitedOverThisConnection = -36,
        FmipProtected = -37,
        McProtected = -38,
        McChallengeRequired = -39,
        UnknownError = -256
    }

    internal static class LockdownErrorMethods
    {
        public static bool IsError(this LockdownError err)
        {
            return err != LockdownError.Success;
        }

        public static LockdownException? GetException(this LockdownError err)
        {
            if (err.IsError()) {
                return new LockdownException(err);
            }
            return null;
        }
    }
}
