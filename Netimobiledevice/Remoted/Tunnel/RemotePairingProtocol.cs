using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Netimobiledevice.Remoted.Tunnel;

public abstract class RemotePairingProtocol : StartTcpTunnel {
    private const int WIRE_PROTOCOL_VERSION = 19;

    private int _sequenceNumber;
    private int _encryptedSequenceNumber;

    public Dictionary<string, object> HandshakeInfo { get; set; } = [];
    public string RemoteDeviceModel => ((Dictionary<string, object>) HandshakeInfo["peerDeviceInfo"])["model"].ToString() ?? string.Empty;
    public override string RemoteIdentifier => ((Dictionary<string, object>) HandshakeInfo["peerDeviceInfo"])["identifier"].ToString() ?? string.Empty;

    public RemotePairingProtocol() : base() { }

    private void AttemptPairVerify() {
        /* TODO
        self.handshake_info = await self._send_receive_handshake({
            'hostOptions': {'attemptPairVerify': True},
            'wireProtocolVersion': XpcInt64Type(self.WIRE_PROTOCOL_VERSION)})
        */
    }

    private async Task AttemptPairVerifyAsync() {
        /* TODO
        self.handshake_info = await self._send_receive_handshake({
            'hostOptions': {'attemptPairVerify': True},
            'wireProtocolVersion': XpcInt64Type(self.WIRE_PROTOCOL_VERSION)})
        */
    }

    private async Task CreateRemoteUnlock() {
        /* TODO
        async def _create_remote_unlock(self) -> None:
        try:
            response = await self._send_receive_encrypted_request({'request': {'_0': {'createRemoteUnlockKey': {}}}})
            self.remote_unlock_host_key = response['createRemoteUnlockKey']['hostKey']
        except PyMobileDevice3Exception:
            # tvOS does not support remote unlock.
            self.remote_unlock_host_key = ''
        */
    }

    private void InitClientServerMainEncryptionKeys() {
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

    private void InitSrpContext(PairConsentResult pairingConsentResult) {
        /* TODO
        # Receive server public and salt and process them.
        pin = pairing_consent_result.pin or '000000'
        client_session = SRPClientSession(
            SRPContext('Pair-Setup', password=pin, prime=PRIME_3072, generator=PRIME_3072_GEN,
                       hash_func=hashlib.sha512))
        client_session.process(pairing_consent_result.public_key.hex(),
                               pairing_consent_result.salt.hex())
        self.srp_context = client_session
        self.encryption_key = binascii.unhexlify(self.srp_context.key)
        */
    }

    private void Pair() {
        PairConsentResult pairingConsentResult = RequestPairConsent();
        InitSrpContext(pairingConsentResult);
        VerifyProof();
        SavePairRecordOnPeer();
        InitClientServerMainEncryptionKeys();
        CreateRemoteUnlock();
        SavePairRecord();
    }

    private async Task PairAsync() {
        PairConsentResult pairingConsentResult = await RequestPairConsentAsync().ConfigureAwait(false);
        InitSrpContext(pairingConsentResult);
        await VerifyProof().ConfigureAwait(false);
        await SavePairRecordOnPeer().ConfigureAwait(false);
        InitClientServerMainEncryptionKeys();
        await CreateRemoteUnlock().ConfigureAwait(false);
        SavePairRecord();
    }

    private async Task<Dictionary<string, object>> ReceivePlainResponse() {
        Dictionary<string, object> response = await ReceiveResponse().ConfigureAwait(false);
        Dictionary<string, object> message = (Dictionary<string, object>) response["message"];
        Dictionary<string, object> plain = (Dictionary<string, object>) message["plain"];
        Dictionary<string, object> result = (Dictionary<string, object>) plain["_0"];
        return result;
    }

    /// <summary>
    /// Displays a Trust / Don't Trust dialog
    /// </summary>
    /// <returns></returns>
    private PairConsentResult RequestPairConsent() {
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

    /// <summary>
    /// Displays a Trust / Don't Trust dialog
    /// </summary>
    /// <returns></returns>
    private async Task<PairConsentResult> RequestPairConsentAsync() {
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

    private void SavePairRecord() {
        /* TODO
    def save_pair_record(self) -> None:
        self.pair_record_path.write_bytes(
            plistlib.dumps({
                'public_key': self.ed25519_private_key.public_key().public_bytes_raw(),
                'private_key': self.ed25519_private_key.private_bytes_raw(),
                'remote_unlock_host_key': self.remote_unlock_host_key
            }))
        OSUTIL.chown_to_non_sudo_if_needed(self.pair_record_path)
        */
    }

    private async Task SavePairRecordOnPeer() {
        /* TODO
 async def _save_pair_record_on_peer(self) -> dict:
        # HKDF with above computed key (SRP_compute_key) + Pair-Setup-Encrypt-Salt + Pair-Setup-Encrypt-Info
        # result used as key for chacha20-poly1305
        setup_encryption_key = HKDF(
            algorithm=hashes.SHA512(),
            length=32,
            salt=b'Pair-Setup-Encrypt-Salt',
            info=b'Pair-Setup-Encrypt-Info',
        ).derive(self.encryption_key)

        self.ed25519_private_key = Ed25519PrivateKey.generate()

        # HKDF with above computed key:
        #   (SRP_compute_key) + Pair-Setup-Controller-Sign-Salt + Pair-Setup-Controller-Sign-Info
        signbuf = HKDF(
            algorithm=hashes.SHA512(),
            length=32,
            salt=b'Pair-Setup-Controller-Sign-Salt',
            info=b'Pair-Setup-Controller-Sign-Info',
        ).derive(self.encryption_key)

        signbuf += self.identifier.encode()
        signbuf += self.ed25519_private_key.public_key().public_bytes_raw()

        self.signature = self.ed25519_private_key.sign(signbuf)

        device_info = dumps({
            'altIRK': b'\xe9\xe8-\xc0jIykVoT\x00\x19\xb1\xc7{',
            'btAddr': '11:22:33:44:55:66',
            'mac': b'\x11\x22\x33\x44\x55\x66',
            'remotepairing_serial_number': 'AAAAAAAAAAAA',
            'accountID': self.identifier,
            'model': 'computer-model',
            'name': platform.node()
        })

        tlv = PairingDataComponentTLVBuf.build([
            {'type': PairingDataComponentType.IDENTIFIER, 'data': self.identifier.encode()},
            {'type': PairingDataComponentType.PUBLIC_KEY,
             'data': self.ed25519_private_key.public_key().public_bytes_raw()},
            {'type': PairingDataComponentType.SIGNATURE, 'data': self.signature},
            {'type': PairingDataComponentType.INFO, 'data': device_info},
        ])

        cip = ChaCha20Poly1305(setup_encryption_key)
        encrypted_data = cip.encrypt(b'\x00\x00\x00\x00PS-Msg05', tlv, b'')

        tlv = PairingDataComponentTLVBuf.build([
            {'type': PairingDataComponentType.ENCRYPTED_DATA, 'data': encrypted_data[:255]},
            {'type': PairingDataComponentType.ENCRYPTED_DATA, 'data': encrypted_data[255:]},
            {'type': PairingDataComponentType.STATE, 'data': b'\x05'},
        ])

        response = await self._send_receive_pairing_data({
            'data': tlv,
            'kind': 'setupManualPairing',
            'sendingHost': platform.node(),
            'startNewSession': False})
        data = self.decode_tlv(PairingDataComponentTLVBuf.parse(response))

        tlv = PairingDataComponentTLVBuf.parse(cip.decrypt(
            b'\x00\x00\x00\x00PS-Msg06', data[PairingDataComponentType.ENCRYPTED_DATA], b''))

        return tlv
        */
    }

    private async Task SendReceiveHandshake() {
        /* TODO

    async def _send_receive_handshake(self, handshake_data: dict) -> dict:
        response = await self._send_receive_plain_request({'request': {'_0': {'handshake': {'_0': handshake_data}}}})
        return response['response']['_1']['handshake']['_0']
        */
    }

    private async Task SendReceivePlainRequest() {
        /* TODO
    async def _send_receive_plain_request(self, plain_request: dict):
        await self._send_plain_request(plain_request)
        return await self._receive_plain_response()
        */
    }

    private bool ValidatePairing() {
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

    private async Task<bool> ValidatePairingAsync() {
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

    private async Task VerifyProof() {
        /* TODO
    async def _verify_proof(self) -> None:
        client_public = binascii.unhexlify(self.srp_context.public)
        client_session_key_proof = binascii.unhexlify(self.srp_context.key_proof)

        tlv = PairingDataComponentTLVBuf.build([
            {'type': PairingDataComponentType.STATE, 'data': b'\x03'},
            {'type': PairingDataComponentType.PUBLIC_KEY, 'data': client_public[:255]},
            {'type': PairingDataComponentType.PUBLIC_KEY, 'data': client_public[255:]},
            {'type': PairingDataComponentType.PROOF, 'data': client_session_key_proof},
        ])

        response = await self._send_receive_pairing_data({
            'data': tlv,
            'kind': 'setupManualPairing',
            'sendingHost': platform.node(),
            'startNewSession': False})
        data = self.decode_tlv(PairingDataComponentTLVBuf.parse(response))
        assert self.srp_context.verify_proof(data[PairingDataComponentType.PROOF].hex().encode())
        */
    }

    public abstract Task CloseAsync();

    public virtual void Connect(bool autopair = true) {
        AttemptPairVerify();
        if (ValidatePairing()) {
            // Pairing record validation succeeded, so we can initiate the relevant session keys
            InitClientServerMainEncryptionKeys();
            return;
        }

        if (autopair) {
            Pair();
            Close();

            // Once pairing is completed, the remote endpoint closes the connection, so it must be re-established
            throw new RemotePairingCompletedException();
        }
    }

    public virtual async Task ConnectAsync(bool autopair = true) {
        await AttemptPairVerifyAsync().ConfigureAwait(false);

        if (await ValidatePairingAsync().ConfigureAwait(false)) {
            // Pairing record validation succeeded, so we can initiate the relevant session keys
            InitClientServerMainEncryptionKeys();
            return;
        }

        if (autopair) {
            await PairAsync().ConfigureAwait(false);
            await CloseAsync().ConfigureAwait(false);

            // Once pairing is completed, the remote endpoint closes the connection, so it must be re-established
            throw new RemotePairingCompletedException();
        }
    }

    public abstract Task<Dictionary<string, object>> ReceiveResponse();

    public abstract Task SendRequest(Dictionary<string, object> data);

    public async Task<Dictionary<string, object>> SendReceiveRequest(Dictionary<string, object> data) {
        await SendRequest(data);
        return await ReceiveResponse();
    }

    public async Task<TunnelResult> StartQuicTunnel() {
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
         */
    }

    public async Task<TunnelResult> StartTcpTunnel() {
        /* TODO
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
}
