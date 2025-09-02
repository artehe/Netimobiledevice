using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel;

public abstract class RemotePairingProtocol : StartTcpTunnel
{
    private const int WIRE_PROTOCOL_VERSION = 19;

    private int _sequenceNumber;
    private int _encryptedSequenceNumber;

    public dynamic? HandshakeInfo { get; set; }
    public string RemoteDeviceModel => HandshakeInfo["peerDeviceInfo"]["model"].ToString();
    public override string RemoteIdentifier => HandshakeInfo["peerDeviceInfo"]["identifier"];

    public RemotePairingProtocol() : base() { }

    private async Task AttemptPairVerify()
    {
        /* TODO
        self.handshake_info = await self._send_receive_handshake({
            'hostOptions': {'attemptPairVerify': True},
            'wireProtocolVersion': XpcInt64Type(self.WIRE_PROTOCOL_VERSION)})
        */
    }

    private void InitClientServerMainEncryptionKeys()
    {
        /* TODO
    def _init_client_server_main_encryption_keys(self) -> None:
        client_key = HKDF(
            algorithm=hashes.SHA512(),
            length=32,
            salt=None,
            info=b'ClientEncrypt-main',
        ).derive(self.encryption_key)
        self.client_cip = ChaCha20Poly1305(client_key)

        server_key = HKDF(
            algorithm=hashes.SHA512(),
            length=32,
            salt=None,
            info=b'ServerEncrypt-main',
        ).derive(self.encryption_key)
        self.server_cip = ChaCha20Poly1305(server_key)
        */
    }

    private async Task Pair()
    {
        /* TODO
        PairConsentResult pairingConsentResult = await RequestPairConsent().ConfigureAwait(false);
        InitSrpContext(pairingConsentResult);
        await VerifyProof().ConfigureAwait(false);
        await SavePairRecordOnPeer().ConfigureAwait(false);
        InitClientServerMainEncryptionKeys();
        await CreateRemoteUnlock().ConfigureAwait(false);
        SavePairRecord();
        */
    }

    private async Task<Dictionary<string, object>> ReceivePlainResponse()
    {
        Dictionary<string, object> response = await ReceiveResponse().ConfigureAwait(false);
        // TODO return response["message"]["plain"]["_0"];
        return response;
    }

    /// <summary>
    /// Displays a Trust / Don't Trust dialog
    /// </summary>
    /// <returns></returns>
    private async Task<PairConsentResult> RequestPairConsent()
    {
        throw new NotImplementedException();
        /* TODO
     async def _request_pair_consent(self) -> PairConsentResult:
        tlv = PairingDataComponentTLVBuf.build([
            {'type': PairingDataComponentType.METHOD, 'data': b'\x00'},
            {'type': PairingDataComponentType.STATE, 'data': b'\x01'},
        ])

        await self._send_pairing_data({'data': tlv,
                                       'kind': 'setupManualPairing',
                                       'sendingHost': platform.node(),
                                       'startNewSession': True})
        self.logger.info('Waiting user pairing consent')
        response = await self._receive_plain_response()
        response = response['event']['_0']

        pin = None
        if 'pairingRejectedWithError' in response:
            raise PairingError(
                response['pairingRejectedWithError']['wrappedError']['userInfo']['NSLocalizedDescription'])
        elif 'awaitingUserConsent' in response:
            pairing_data = await self._receive_pairing_data()
        else:
            # On tvOS no consent is needed and pairing data is returned immediately.
            pairing_data = self._decode_bytes_if_needed(response['pairingData']['_0']['data'])
            # On tvOS we need pin to setup pairing.
            if 'AppleTV' in self.remote_device_model:
                pin = input('Enter PIN: ')

        data = self.decode_tlv(PairingDataComponentTLVBuf.parse(pairing_data))
        return PairConsentResult(public_key=data[PairingDataComponentType.PUBLIC_KEY],
                                 salt=data[PairingDataComponentType.SALT],
                                 pin=pin)
        */
    }

    private async Task SendReceiveHandshake()
    {
        /* TODO

    async def _send_receive_handshake(self, handshake_data: dict) -> dict:
        response = await self._send_receive_plain_request({'request': {'_0': {'handshake': {'_0': handshake_data}}}})
        return response['response']['_1']['handshake']['_0']
        */
    }

    private async Task SendReceivePlainRequest()
    {
        /* TODO
    async def _send_receive_plain_request(self, plain_request: dict):
        await self._send_plain_request(plain_request)
        return await self._receive_plain_response()
        */
    }

    private async Task<bool> ValidatePairing()
    {
        /* TODO
    async def _validate_pairing(self) -> bool:
        pairing_data = PairingDataComponentTLVBuf.build([
            {'type': PairingDataComponentType.STATE, 'data': b'\x01'},
            {'type': PairingDataComponentType.PUBLIC_KEY,
             'data': self.x25519_private_key.public_key().public_bytes_raw()},
        ])
        response = await self._send_receive_pairing_data({'data': pairing_data,
                                                          'kind': 'verifyManualPairing',
                                                          'startNewSession': True})
        data = self.decode_tlv(PairingDataComponentTLVBuf.parse(response))

        if PairingDataComponentType.ERROR in data:
            await self._send_pair_verify_failed()
            return False

        peer_public_key = X25519PublicKey.from_public_bytes(data[PairingDataComponentType.PUBLIC_KEY])
        self.encryption_key = self.x25519_private_key.exchange(peer_public_key)

        derived_key = HKDF(
            algorithm=hashes.SHA512(),
            length=32,
            salt=b'Pair-Verify-Encrypt-Salt',
            info=b'Pair-Verify-Encrypt-Info',
        ).derive(self.encryption_key)
        cip = ChaCha20Poly1305(derived_key)

        # TODO:
        #   we should be able to verify from the received encrypted data, but from some reason we failed to
        #   do so. instead, we verify using the next stage

        if self.pair_record is None:
            private_key = Ed25519PrivateKey.from_private_bytes(b'\x00' * 0x20)
        else:
            private_key = Ed25519PrivateKey.from_private_bytes(self.pair_record['private_key'])

        signbuf = b''
        signbuf += self.x25519_private_key.public_key().public_bytes_raw()
        signbuf += self.identifier.encode()
        signbuf += peer_public_key.public_bytes_raw()

        signature = private_key.sign(signbuf)

        encrypted_data = cip.encrypt(b'\x00\x00\x00\x00PV-Msg03', PairingDataComponentTLVBuf.build([
            {'type': PairingDataComponentType.IDENTIFIER, 'data': self.identifier.encode()},
            {'type': PairingDataComponentType.SIGNATURE, 'data': signature},
        ]), b'')

        pairing_data = PairingDataComponentTLVBuf.build([
            {'type': PairingDataComponentType.STATE, 'data': b'\x03'},
            {'type': PairingDataComponentType.ENCRYPTED_DATA, 'data': encrypted_data},
        ])

        response = await self._send_receive_pairing_data({
            'data': pairing_data,
            'kind': 'verifyManualPairing',
            'startNewSession': False})
        data = self.decode_tlv(PairingDataComponentTLVBuf.parse(response))

        if PairingDataComponentType.ERROR in data:
            await self._send_pair_verify_failed()
            return False

        */
        return true;
    }

    public abstract Task CloseAsync();

    public async Task Connect(bool autopair = true)
    {
        await AttemptPairVerify().ConfigureAwait(false);

        if (await ValidatePairing().ConfigureAwait(false)) {
            // Pairing record validation succeeded, so we can initiate the relevant session keys
            InitClientServerMainEncryptionKeys();
            return;
        }

        if (autopair) {
            await Pair().ConfigureAwait(false);
            await CloseAsync().ConfigureAwait(false);

            // Once pairing is completed, the remote endpoint closes the connection, so it must be re-established
            throw new RemotePairingCompletedException();
        }
    }

    public abstract Task<Dictionary<string, object>> ReceiveResponse();

    public abstract Task SendRequest(Dictionary<string, object> data);

    public async Task<Dictionary<string, object>> SendReceiveRequest(Dictionary<string, object> data)
    {
        await SendRequest(data);
        return await ReceiveResponse();
    }

    /* TODO
    @asynccontextmanager
    async def start_quic_tunnel(
            self, secrets_log_file: Optional[TextIO] = None,
            max_idle_timeout: float = RemotePairingQuicTunnel.MAX_IDLE_TIMEOUT) -> AsyncGenerator[TunnelResult, None]:
        private_key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
        parameters = await self.create_quic_listener(private_key)
        cert = make_cert(private_key, private_key.public_key())
        configuration = QuicConfiguration(
            alpn_protocols=['RemotePairingTunnelProtocol'],
            is_client=True,
            verify_mode=VerifyMode.CERT_NONE,
            verify_hostname=False,
            max_datagram_frame_size=RemotePairingQuicTunnel.MAX_QUIC_DATAGRAM,
            idle_timeout=max_idle_timeout
        )
        configuration.load_cert_chain(cert.public_bytes(Encoding.PEM),
                                      private_key.private_bytes(Encoding.PEM, PrivateFormat.TraditionalOpenSSL,
                                                                NoEncryption()).decode())
        configuration.secrets_log_file = secrets_log_file

        host = self.hostname
        port = parameters['port']

        self.logger.debug(f'Connecting to {host}:{port}')
        try:
            async with aioquic_connect(
                    host,
                    port,
                    configuration=configuration,
                    create_protocol=RemotePairingQuicTunnel,
            ) as client:
                self.logger.debug('quic connected')
                client = cast(RemotePairingQuicTunnel, client)
                await client.wait_connected()
                handshake_response = await client.request_tunnel_establish()
                client.start_tunnel(handshake_response['clientParameters']['address'],
                                    handshake_response['clientParameters']['mtu'],
                                    interface_name=f'{DEFAULT_INTERFACE_NAME}-{self.remote_identifier}')
                try:
                    yield TunnelResult(
                        client.tun.name, handshake_response['serverAddress'], handshake_response['serverRSDPort'],
                        TunnelProtocol.QUIC, client)
                finally:
                    await client.stop_tunnel()
        except ConnectionError:
            raise QuicProtocolNotSupportedError(
                'iOS 18.2+ removed QUIC protocol support. Use TCP instead (requires python3.13+)')

    @asynccontextmanager
    async def start_tcp_tunnel(self) -> AsyncGenerator[TunnelResult, None]:
        parameters = await self.create_tcp_listener()
        host = self.hostname
        port = parameters['port']
        sock = create_connection((host, port))
        OSUTIL.set_keepalive(sock)
        if sys.version_info >= (3, 13):
            ctx = ssl.SSLContext(ssl.PROTOCOL_TLSv1_2)
            ctx.check_hostname = False
            ctx.verify_mode = ssl.CERT_NONE
            ctx.set_ciphers('PSK')
            ctx.set_psk_client_callback(lambda hint: (None, self.encryption_key))
        else:
            # TODO: remove this when python3.12 becomes deprecated
            ctx = SSLPSKContext(ssl.PROTOCOL_TLSv1_2)
            ctx.psk = self.encryption_key
            ctx.set_ciphers('PSK')
        reader, writer = await asyncio.open_connection(sock=sock, ssl=ctx, server_hostname='')
        tunnel = RemotePairingTcpTunnel(reader, writer)
        handshake_response = await tunnel.request_tunnel_establish()

        tunnel.start_tunnel(handshake_response['clientParameters']['address'],
                            handshake_response['clientParameters']['mtu'],
                            interface_name=f'{DEFAULT_INTERFACE_NAME}-{self.remote_identifier}')

        try:
            yield TunnelResult(
                tunnel.tun.name, handshake_response['serverAddress'], handshake_response['serverRSDPort'],
                TunnelProtocol.TCP, tunnel)
        finally:
            await tunnel.stop_tunnel()

     */
}
