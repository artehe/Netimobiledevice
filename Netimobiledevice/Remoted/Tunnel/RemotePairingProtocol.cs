namespace Netimobiledevice.Remoted.Tunnel
{
    public abstract class RemotePairingProtocol
    {
    }

    /* TODO
class RemotePairingProtocol(StartTcpTunnel):
    WIRE_PROTOCOL_VERSION = 19

    def __init__(self):
        self.hostname: Optional[str] = None
        self._sequence_number = 0
        self._encrypted_sequence_number = 0
        self.version = None
        self.handshake_info = None
        self.x25519_private_key = X25519PrivateKey.generate()
        self.ed25519_private_key = Ed25519PrivateKey.generate()
        self.identifier = generate_host_id()
        self.srp_context = None
        self.encryption_key = None
        self.signature = None
        self.logger = logging.getLogger(self.__class__.__name__)

    @abstractmethod
    async def close(self) -> None:
        pass

    @abstractmethod
    async def receive_response(self) -> dict:
        pass

    @abstractmethod
    async def send_request(self, data: dict) -> None:
        pass

    async def send_receive_request(self, data: dict) -> dict:
        await self.send_request(data)
        return await self.receive_response()

    async def connect(self, autopair: bool = True) -> None:
        await self._attempt_pair_verify()

        if not await self._validate_pairing():
            if autopair:
                await self._pair()
        self._init_client_server_main_encryption_keys()

    async def create_quic_listener(self, private_key: RSAPrivateKey) -> dict:
        request = {'request': {'_0': {'createListener': {
            'key': base64.b64encode(
                private_key.public_key().public_bytes(Encoding.DER, PublicFormat.SubjectPublicKeyInfo)
            ).decode(),
            'peerConnectionsInfo': [{'owningPID': os.getpid(), 'owningProcessName': 'CoreDeviceService'}],
            'transportProtocolType': 'quic'}}}}

        response = await self._send_receive_encrypted_request(request)
        return response['createListener']

    async def create_tcp_listener(self) -> dict:
        request = {'request': {'_0': {'createListener': {
            'key': base64.b64encode(self.encryption_key).decode(),
            'peerConnectionsInfo': [{'owningPID': os.getpid(), 'owningProcessName': 'CoreDeviceService'}],
            'transportProtocolType': 'tcp'}}}}
        response = await self._send_receive_encrypted_request(request)
        return response['createListener']

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
                                handshake_response['clientParameters']['mtu'])
            try:
                yield TunnelResult(
                    client.tun.name, handshake_response['serverAddress'], handshake_response['serverRSDPort'],
                    TunnelProtocol.QUIC, client)
            finally:
                await client.stop_tunnel()

    @asynccontextmanager
    async def start_tcp_tunnel(self) -> AsyncGenerator[TunnelResult, None]:
        parameters = await self.create_tcp_listener()
        host = self.hostname
        port = parameters['port']
        sock = create_connection((host, port))
        OSUTIL.set_keepalive(sock)
        ctx = SSLPSKContext(ssl.PROTOCOL_TLSv1_2)
        ctx.psk = self.encryption_key
        ctx.set_ciphers('PSK')
        reader, writer = await asyncio.open_connection(sock=sock, ssl=ctx, server_hostname='')
        tunnel = RemotePairingTcpTunnel(reader, writer)
        handshake_response = await tunnel.request_tunnel_establish()

        tunnel.start_tunnel(handshake_response['clientParameters']['address'],
                            handshake_response['clientParameters']['mtu'])

        try:
            yield TunnelResult(
                tunnel.tun.name, handshake_response['serverAddress'], handshake_response['serverRSDPort'],
                TunnelProtocol.TCP, tunnel)
        finally:
            await tunnel.stop_tunnel()

    def save_pair_record(self) -> None:
        self.pair_record_path.write_bytes(
            plistlib.dumps({
                'public_key': self.ed25519_private_key.public_key().public_bytes_raw(),
                'private_key': self.ed25519_private_key.private_bytes_raw(),
                'remote_unlock_host_key': self.remote_unlock_host_key
            }))
        OSUTIL.chown_to_non_sudo_if_needed(self.pair_record_path)

    @property
    def pair_record(self) -> Optional[dict]:
        if self.pair_record_path.exists():
            return plistlib.loads(self.pair_record_path.read_bytes())
        return None

    @property
    def remote_identifier(self) -> str:
        return self.handshake_info['peerDeviceInfo']['identifier']

    @property
    def remote_device_model(self) -> str:
        return self.handshake_info['peerDeviceInfo']['model']

    @property
    def pair_record_path(self) -> Path:
        pair_records_cache_directory = create_pairing_records_cache_folder()
        return (pair_records_cache_directory /
                f'{get_remote_pairing_record_filename(self.remote_identifier)}.{PAIRING_RECORD_EXT}')

    async def _pair(self) -> None:
        pairing_consent_result = await self._request_pair_consent()
        self._init_srp_context(pairing_consent_result)
        await self._verify_proof()
        await self._save_pair_record_on_peer()
        self._init_client_server_main_encryption_keys()
        await self._create_remote_unlock()
        self.save_pair_record()

    async def _request_pair_consent(self) -> PairConsentResult:
        """ Display a Trust / Don't Trust dialog """

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

    def _init_srp_context(self, pairing_consent_result: PairConsentResult) -> None:
        # Receive server public and salt and process them.
        pin = pairing_consent_result.pin or '000000'
        client_session = SRPClientSession(
            SRPContext('Pair-Setup', password=pin, prime=PRIME_3072, generator=PRIME_3072_GEN,
                       hash_func=hashlib.sha512))
        client_session.process(pairing_consent_result.public_key.hex(),
                               pairing_consent_result.salt.hex())
        self.srp_context = client_session
        self.encryption_key = binascii.unhexlify(self.srp_context.key)

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

    async def _create_remote_unlock(self) -> None:
        try:
            response = await self._send_receive_encrypted_request({'request': {'_0': {'createRemoteUnlockKey': {}}}})
            self.remote_unlock_host_key = response['createRemoteUnlockKey']['hostKey']
        except PyMobileDevice3Exception:
            # tvOS does not support remote unlock.
            self.remote_unlock_host_key = ''

    async def _attempt_pair_verify(self) -> None:
        self.handshake_info = await self._send_receive_handshake({
            'hostOptions': {'attemptPairVerify': True},
            'wireProtocolVersion': XpcInt64Type(self.WIRE_PROTOCOL_VERSION)})

    @staticmethod
    def _decode_bytes_if_needed(data: bytes) -> bytes:
        return data

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

        return True

    async def _send_pair_verify_failed(self) -> None:
        await self._send_plain_request({'event': {'_0': {'pairVerifyFailed': {}}}})

    async def _send_receive_encrypted_request(self, request: dict) -> dict:
        nonce = Int64ul.build(self._encrypted_sequence_number) + b'\x00' * 4
        encrypted_data = self.client_cip.encrypt(
            nonce,
            json.dumps(request).encode(),
            b'')

        response = await self.send_receive_request({'message': {
            'streamEncrypted': {'_0': encrypted_data}},
            'originatedBy': 'host',
            'sequenceNumber': XpcUInt64Type(self._sequence_number)})
        self._encrypted_sequence_number += 1

        encrypted_data = self._decode_bytes_if_needed(response['message']['streamEncrypted']['_0'])
        plaintext = self.server_cip.decrypt(nonce, encrypted_data, None)
        response = json.loads(plaintext)['response']['_1']

        if 'errorExtended' in response:
            raise PyMobileDevice3Exception(response['errorExtended']['_0']['userInfo']['NSLocalizedDescription'])

        return response

    async def _send_receive_handshake(self, handshake_data: dict) -> dict:
        response = await self._send_receive_plain_request({'request': {'_0': {'handshake': {'_0': handshake_data}}}})
        return response['response']['_1']['handshake']['_0']

    async def _send_receive_pairing_data(self, pairing_data: dict) -> bytes:
        await self._send_pairing_data(pairing_data)
        return await self._receive_pairing_data()

    async def _send_pairing_data(self, pairing_data: dict) -> None:
        await self._send_plain_request({'event': {'_0': {'pairingData': {'_0': pairing_data}}}})

    async def _receive_pairing_data(self) -> bytes:
        response = await self._receive_plain_response()
        response = response['event']['_0']
        if 'pairingData' in response:
            return self._decode_bytes_if_needed(response['pairingData']['_0']['data'])
        if 'pairingRejectedWithError' in response:
            raise UserDeniedPairingError(response['pairingRejectedWithError']
                                         .get('wrappedError', {})
                                         .get('userInfo', {})
                                         .get('NSLocalizedDescription'))
        raise PyMobileDevice3Exception(f'Got an unknown state message: {response}')

    async def _send_receive_plain_request(self, plain_request: dict):
        await self._send_plain_request(plain_request)
        return await self._receive_plain_response()

    async def _send_plain_request(self, plain_request: dict) -> None:
        await self.send_request({'message': {'plain': {'_0': plain_request}},
                                 'originatedBy': 'host',
                                 'sequenceNumber': XpcUInt64Type(self._sequence_number)})
        self._sequence_number += 1

    async def _receive_plain_response(self) -> dict:
        response = await self.receive_response()
        return response['message']['plain']['_0']

    @staticmethod
    def decode_tlv(tlv_list: list[Container]) -> dict:
        result = {}
        for tlv in tlv_list:
            if tlv.type in result:
                result[tlv.type] += tlv.data
            else:
                result[tlv.type] = tlv.data
        return result

    async def __aenter__(self) -> 'CoreDeviceTunnelService':
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb) -> None:
        await self.close()

     */
}
