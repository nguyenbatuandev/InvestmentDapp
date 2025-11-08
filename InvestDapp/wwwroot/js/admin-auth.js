(function () {
    const loginButton = document.getElementById('btnLogin');
    const statusLabel = document.getElementById('statusMessage');
    const antiforgeryInput = document.querySelector('#antiforgeryForm input[name="__RequestVerificationToken"]');
    const config = window.__ADMIN_LOGIN_CONFIG__ || {};

    if (!loginButton || !statusLabel || !antiforgeryInput || !config.nonceEndpoint || !config.verifyEndpoint) {
        console.error('Admin login component is not initialised correctly');
        return;
    }

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

    // Combined login function - one click does everything
    loginButton.addEventListener('click', async () => {
        const ethereum = requireEthereum();
        if (!ethereum) {
            return;
        }

        try {
            loginButton.disabled = true;
            
            // Step 1: Request MetaMask accounts
            setStatus('Đang kết nối MetaMask...', null);
            const accounts = await ethereum.request({ method: 'eth_requestAccounts' });
            if (!accounts || !accounts.length) {
                setStatus('Không nhận được địa chỉ ví.', 'error');
                loginButton.disabled = false;
                return;
            }

            const currentAccount = accounts[0];
            console.log('✅ Wallet connected:', currentAccount);

            // Step 2: Fetch nonce
            setStatus('Đang tạo nonce...', null);
            const nonce = await fetchNonce(currentAccount);
            console.log('✅ Nonce received');

            // Step 3: Request signature
            setStatus('Vui lòng ký trong MetaMask...', null);
            const signature = await ethereum.request({
                method: 'personal_sign',
                params: [nonce, currentAccount]
            });
            console.log('✅ Signature obtained');

            // Step 4: Verify signature
            setStatus('Đang xác thực...', null);
            const redirectUrl = await verifySignature(currentAccount, signature);
            
            setStatus('Đăng nhập thành công! Đang chuyển hướng...', 'success');
            setTimeout(() => {
                window.location.href = redirectUrl || config.dashboardUrl || '/admin';
            }, 500);

        } catch (error) {
            console.error('❌ Login error:', error);
            setStatus(error.message || 'Đăng nhập thất bại. Vui lòng thử lại.', 'error');
            loginButton.disabled = false;
        }
    });
})();
