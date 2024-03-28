namespace Netimobiledevice.Lockdown
{
    public class UsbmuxLockdownClient : LockdownClient
    {
        /* TODO
    def __init__(self, service: ServiceConnection, host_id: str, identifier: str = None,
                 label: str = DEFAULT_LABEL, system_buid: str = SYSTEM_BUID, pair_record: Mapping = None,
                 pairing_records_cache_folder: Path = None, port: int = SERVICE_PORT,
                 usbmux_address: Optional[str] = None):
        super().__init__(service, host_id, identifier, label, system_buid, pair_record, pairing_records_cache_folder,
                         port)
        self.usbmux_address = usbmux_address

    @property
    def short_info(self) -> Dict:
        short_info = super().short_info
        short_info['ConnectionType'] = self.service.mux_device.connection_type
        return short_info

    def fetch_pair_record(self) -> None:
        if self.identifier is not None:
            self.pair_record = get_preferred_pair_record(self.identifier, self.pairing_records_cache_folder,
                                                         usbmux_address=self.usbmux_address)

    def _create_service_connection(self, port: int) -> ServiceConnection:
        return ServiceConnection.create_using_usbmux(self.identifier, port,
                                                     self.service.mux_device.connection_type,
                                                     usbmux_address=self.usbmux_address)        
        */
    }
}
