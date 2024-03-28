namespace Netimobiledevice.Lockdown
{
    public class PlistUsbmuxLockdownClient : UsbmuxLockdownClient
    {
        public override void SavePairRecord()
        {
            base.SavePairRecord();
            record_data = plistlib.dumps(self.pair_record);
            with usbmux.create_mux() as client:
                client.save_pair_record(self.identifier, self.service.mux_device.devid, record_data)
        }
    }
}
