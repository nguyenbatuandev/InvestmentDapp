(function () {
    const connectButton = document.getElementById('btnConnect');
    const signButton = document.getElementById('btnSign');
    const statusLabel = document.getElementById('statusMessage');
    const antiforgeryInput = document.querySelector('#antiforgeryForm input[name="__RequestVerificationToken"]');
    const config = window.__ADMIN_LOGIN_CONFIG__ || {};

    if (!connectButton || !signButton || !statusLabel || !antiforgeryInput || !config.nonceEndpoint || !config.verifyEndpoint) {
        console.error('Admin login component is not initialised correctly');
        return;
    }

    let currentAccount = null;
    let cachedNonce = null;

    const setStatus = (message, type) => {
        statusLabel.textContent = message;
        statusLabel.className = 'status-line' + (type ? ` ${type}` : '');
    };

    const requireEthereum = () => {
        const { ethereum } = window;
        if (!ethereum) {
            setStatus('Không tìm thấy MetaMask. Vui lòng cài đặt tiện ích.', 'error');
            return null;
        }
        return ethereum;
    };

    const fetchNonce = async (wallet) => {
        try {
            const response = await fetch(config.nonceEndpoint, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': antiforgeryInput.value
                },
                body: JSON.stringify({ walletAddress: wallet })
            });

            const payload = await response.json();
            if (!response.ok || !payload.success) {
                throw new Error(payload.error || 'Không thể tạo nonce.');
            }
            return payload.nonce;
        } catch (error) {
            throw error;
        }
    };

    const verifySignature = async (wallet, signature) => {
        const response = await fetch(config.verifyEndpoint, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': antiforgeryInput.value
            },
            body: JSON.stringify({ walletAddress: wallet, signature })
        });

        const payload = await response.json();
        if (!response.ok || !payload.success) {
            throw new Error(payload.error || 'Không thể xác minh chữ ký.');
        }
        return payload.redirect;
    };

    connectButton.addEventListener('click', async () => {
        const ethereum = requireEthereum();
        if (!ethereum) {
            return;
        }

        try {
            setStatus('Đang yêu cầu MetaMask...', null);
            const accounts = await ethereum.request({ method: 'eth_requestAccounts' });
            if (!accounts || !accounts.length) {
                setStatus('Không nhận được địa chỉ ví.', 'error');
                return;
            }

            currentAccount = accounts[0];
            setStatus(`Ví đang được sử dụng: ${currentAccount}`, 'success');
            signButton.disabled = false;
            cachedNonce = await fetchNonce(currentAccount);
            setStatus('Nonce đã sẵn sàng, hãy ký để hoàn tất đăng nhập.', 'success');
        } catch (error) {
            console.error(error);
            setStatus(error.message || 'Không thể kết nối MetaMask.', 'error');
            signButton.disabled = true;
            cachedNonce = null;
        }
    });

    signButton.addEventListener('click', async () => {
        const ethereum = requireEthereum();
        if (!ethereum) {
            return;
        }

        if (!currentAccount) {
            setStatus('Vui lòng kết nối ví trước.', 'error');
            return;
        }

        if (!cachedNonce) {
            setStatus('Nonce không khả dụng, hãy lấy lại nonce.', 'error');
            return;
        }

        try {
            signButton.disabled = true;
            setStatus('Đang yêu cầu chữ ký...', null);
            const signature = await ethereum.request({
                method: 'personal_sign',
                params: [cachedNonce, currentAccount]
            });

            setStatus('Đang xác thực...', null);
            const redirectUrl = await verifySignature(currentAccount, signature);
            setStatus('Đăng nhập thành công. Đang chuyển hướng...', 'success');
            window.location.href = redirectUrl || config.dashboardUrl || '/admin';
        } catch (error) {
            console.error(error);
            setStatus(error.message || 'Đăng nhập thất bại.', 'error');
            signButton.disabled = false;
        }
    });
})();
