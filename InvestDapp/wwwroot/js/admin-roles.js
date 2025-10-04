(function (w, d) {
    'use strict';

    const forms = Array.from(d.querySelectorAll('.role-form'));
    if (!forms.length) {
        return;
    }

    const walletPattern = /^0x[a-fA-F0-9]{40}$/;
    const rolePanel = d.querySelector('.role-panel');
    const blockchainSection = d.querySelector('.role-blockchain-status');
    const walletPanel = d.querySelector('[data-wallet-panel]');
    const connectBtn = walletPanel ? walletPanel.querySelector('[data-wallet-connect]') : null;
    const walletAddressEl = walletPanel ? walletPanel.querySelector('[data-wallet-address]') : null;
    const walletNetworkEl = walletPanel ? walletPanel.querySelector('[data-wallet-network]') : null;
    const flashStorageKey = 'admin-role-flash-message';

    const defaultCdns = [
        'https://cdnjs.cloudflare.com/ajax/libs/ethers/6.10.0/ethers.umd.min.js',
        'https://unpkg.com/ethers@6.10.0/bundles/ethers.umd.min.js'
    ];

    const expectedChainHexRaw = blockchainSection?.dataset.chainHex ?? (w.CONTRACT_CONFIG?.EXPECTED_CHAIN_ID_HEX ?? '');
    const expectedChainDecRaw = blockchainSection?.dataset.chainId ?? w.CONTRACT_CONFIG?.EXPECTED_CHAIN_ID_DEC;
    const contractAddressRaw = (blockchainSection?.dataset.contractAddress ?? '').trim();

    const state = {
        account: null,
        chainId: null,
        networkLabel: null,
        expectedChainHex: sanitizeHex(expectedChainHexRaw),
        expectedChainDec: parseInt(expectedChainDecRaw, 10) || null,
        rpcUrl: (blockchainSection?.dataset.rpcUrl ?? '').trim(),
        contractAddress: contractAddressRaw || (w.CONTRACT_CONFIG?.CONTRACT_ADDRESS || ''),
        readyServer: blockchainSection?.dataset.ready === 'true',
        hasRpc: blockchainSection?.dataset.hasRpc === 'true',
        adminAddress: blockchainSection?.dataset.adminAddress || '',
        ethersLoadingPromise: null,
        eventsBound: false
    };

    const ethersCdns = Array.isArray(w.FALLBACK_ETHERS_CDNS) && w.FALLBACK_ETHERS_CDNS.length
        ? w.FALLBACK_ETHERS_CDNS
        : defaultCdns;

    bootstrapFlash();
    initialiseWalletPanel();
    wireForms();

    function bootstrapFlash() {
        if (!rolePanel) {
            return;
        }

        try {
            const raw = w.sessionStorage.getItem(flashStorageKey);
            if (!raw) {
                return;
            }

            w.sessionStorage.removeItem(flashStorageKey);
            const payload = JSON.parse(raw);
            if (payload && payload.message) {
                injectFlash(payload.type || 'success', payload.message);
            }
        } catch (error) {
            console.warn('Không thể đọc flash message từ sessionStorage.', error);
        }
    }

    function injectFlash(type, message) {
        if (!rolePanel || !message) {
            return;
        }

        const alert = createAlert(type, message);
        const header = rolePanel.querySelector('.role-header');
        if (header && header.parentNode) {
            header.parentNode.insertBefore(alert, header.nextSibling);
        } else {
            rolePanel.insertAdjacentElement('afterbegin', alert);
        }
    }

    function createAlert(type, message) {
        const alert = d.createElement('div');
        alert.className = `role-alert ${mapAlertType(type)}`;
        alert.setAttribute('role', 'alert');
        alert.textContent = message;
        return alert;
    }

    function mapAlertType(type) {
        switch ((type || '').toLowerCase()) {
            case 'danger':
            case 'error':
                return 'danger';
            case 'warning':
                return 'warning';
            default:
                return 'success';
        }
    }

    function storeFlash(type, message) {
        try {
            w.sessionStorage.setItem(flashStorageKey, JSON.stringify({ type, message }));
        } catch (error) {
            console.warn('Không thể lưu flash message.', error);
        }
    }

    function initialiseWalletPanel() {
        if (!connectBtn) {
            return;
        }

        connectBtn.addEventListener('click', async () => {
            try {
                toggleButtonBusy(connectBtn, true, 'Đang kết nối...');
                await ensureWalletConnection();
            } catch (error) {
                console.error(error);
                showWalletError(normalizeErrorMessage(error));
            } finally {
                toggleButtonBusy(connectBtn, false);
            }
        });

        updateWalletPanel();
    }

    function showWalletError(message) {
        if (walletAddressEl) {
            walletAddressEl.textContent = message || 'Chưa kết nối';
        }
        if (walletNetworkEl) {
            walletNetworkEl.textContent = '';
        }
    }

    function updateWalletPanel() {
        if (walletAddressEl) {
            walletAddressEl.textContent = state.account || 'Chưa kết nối';
        }

        if (walletNetworkEl) {
            walletNetworkEl.textContent = formatNetworkLabel();
        }
    }

    function formatNetworkLabel() {
        if (!state.chainId) {
            return '';
        }

        const info = guessChainInfo(state.chainId, state.expectedChainHex);
        const mismatch = state.expectedChainDec && state.chainId !== state.expectedChainDec;
        const hex = toHex(state.chainId);
        const suffix = mismatch ? ' (không khớp cấu hình)' : '';
        if (info.name) {
            return `${info.name} (${hex})${suffix}`;
        }

        return `Chain ${state.chainId} (${hex})${suffix}`;
    }

    function wireForms() {
        forms.forEach(form => {
            const walletInput = form.querySelector('input[name="WalletAddress"]');
            const roleSelect = form.querySelector('select[name="Role"]');
            const errorEl = form.querySelector('[data-role-error]');
            const submitBtn = form.querySelector('button[type="submit"]');
            const isServerMode = form.dataset.blockchainReady === 'true';

            form.addEventListener('submit', function(evt) {
                console.log('[ADMIN-ROLES] Form submit event fired');
                clearInlineError(errorEl);

                if (!walletInput || !roleSelect) {
                    console.log('[ADMIN-ROLES] Missing inputs, allowing default submit');
                    return;
                }

                const trimmed = (walletInput.value || '').trim();
                if (!walletPattern.test(trimmed)) {
                    evt.preventDefault();
                    showInlineError(errorEl, 'Địa chỉ ví phải có dạng 0x + 40 ký tự hex.');
                    walletInput.focus();
                    console.log('[ADMIN-ROLES] Invalid wallet format');
                    return;
                }

                if (!roleSelect.value) {
                    evt.preventDefault();
                    showInlineError(errorEl, 'Vui lòng chọn quyền cần thao tác.');
                    roleSelect.focus();
                    console.log('[ADMIN-ROLES] No role selected');
                    return;
                }

                walletInput.value = trimmed;

                const meta = getRoleMeta(roleSelect);
                const requiresSignature = !!meta.requiresSignature;
                const roleLabel = meta.label;

                console.log('[ADMIN-ROLES] Submit handler:', { 
                    wallet: trimmed, 
                    role: roleSelect.value, 
                    requiresSignature, 
                    roleLabel,
                    isServerMode 
                });

                if (!requiresSignature) {
                    console.log('[ADMIN-ROLES] OFF-CHAIN role detected, allowing natural form submit');
                    setLoading(submitBtn, true, 'Đang lưu...');
                    // DO NOT preventDefault - let form submit normally to server
                    return;
                }

                // For on-chain roles, prevent default and handle client-side
                evt.preventDefault();
                console.log('[ADMIN-ROLES] ON-CHAIN role, prevented default');

                if (isServerMode) {
                    console.log('[ADMIN-ROLES] Server mode, submitting form');
                    setLoading(submitBtn, true, 'Đang gửi...');
                    form.submit();
                    return;
                }

                console.log('[ADMIN-ROLES] Client mode, performing MetaMask mutation');

                // Handle async MetaMask operations
                (async function() {
                    try {
                        setLoading(submitBtn, true, 'Đang ký giao dịch...');
                        await performClientMutation({
                            form,
                            wallet: trimmed,
                            roleValue: roleSelect.value,
                            roleLabel,
                            action: form.dataset.roleAction || 'grant'
                        });
                    } catch (error) {
                        console.error('[ADMIN-ROLES] Error during mutation:', error);
                        showInlineError(errorEl, normalizeErrorMessage(error));
                        setLoading(submitBtn, false);
                    }
                })();
            });

            form.addEventListener('input', () => {
                clearInlineError(errorEl);
                setLoading(submitBtn, false);
            });

            form.addEventListener('change', evt => {
                if (evt.target === roleSelect) {
                    updateRoleModeHint(roleSelect);
                }
            });

            updateRoleModeHint(roleSelect);
        });
    }

    async function performClientMutation(ctx) {
        if (!state.contractAddress) {
            throw new Error('Chưa cấu hình địa chỉ smart contract.');
        }

        const ethersLib = await ensureEthersReady();
        await ensureWalletConnection();

        const contractAbi = getContractAbi();
        if (!Array.isArray(contractAbi) || !contractAbi.length) {
            throw new Error('Chưa cấu hình ABI cho smart contract.');
        }

        const provider = await createEthersProvider(ethersLib);
        const signer = await getEthersSigner(provider);
        const contract = new ethersLib.Contract(state.contractAddress, contractAbi, signer);
        const checksumWallet = checksumAddress(ethersLib, ctx.wallet);

        let tx;
        try {
            tx = await sendContractMutation(contract, ethersLib, {
                roleValue: ctx.roleValue,
                action: ctx.action,
                wallet: checksumWallet
            });
        } catch (error) {
            throw wrapMutationError(error);
        }

        let receipt;
        try {
            receipt = await waitForReceipt(tx);
        } catch (error) {
            throw wrapMutationError(error);
        }

        const transactionHash = receipt?.transactionHash || tx?.hash || tx?.transactionHash;
        const syncResult = await syncWithServer({
            form: ctx.form,
            wallet: checksumWallet,
            roleValue: ctx.roleValue,
            action: ctx.action,
            transactionHash
        });

        const actionLabel = ctx.action === 'revoke' ? 'thu hồi' : 'cấp';
        let message = `Đã ${actionLabel} quyền ${ctx.roleLabel} cho ${checksumWallet}`;
        if (transactionHash) {
            message += `. Tx: ${transactionHash}`;
        }
        if (syncResult.warning) {
            message += `. Lưu ý: ${syncResult.warning}`;
        }

        storeFlash(syncResult.warning ? 'warning' : 'success', message);
        w.location.reload();
    }

    async function sendContractMutation(contract, ethersLib, payload) {
        const { roleValue, action, wallet } = payload;
        const isGrant = action === 'grant';

        if (roleValue === 'Admin') {
            const method = isGrant ? 'grantAdmin' : 'revokeAdmin';
            return contract[method](wallet);
        }

        const roleIdentifier = resolveRoleIdentifier(ethersLib, roleValue);
        if (!roleIdentifier) {
            throw new Error('Vai trò này hiện chưa hỗ trợ ký ví từ trình duyệt.');
        }

        const methodName = isGrant ? 'grantRole' : 'revokeRole';
        return contract[methodName](roleIdentifier, wallet);
    }

    function resolveRoleIdentifier(ethersLib, roleValue) {
        switch (roleValue) {
            case 'SuperAdmin':
                return '0x' + '0'.repeat(64);
            case 'Fundraiser':
                return hashRole(ethersLib, 'CREATOR_ROLE');
            default:
                return null;
        }
    }

    function hashRole(ethersLib, roleName) {
        if (typeof ethersLib.id === 'function') {
            return ethersLib.id(roleName);
        }

        if (ethersLib.utils && typeof ethersLib.utils.id === 'function') {
            return ethersLib.utils.id(roleName);
        }

        throw new Error('Không thể tính hash cho vai trò.');
    }

    function checksumAddress(ethersLib, address) {
        try {
            if (ethersLib.utils && typeof ethersLib.utils.getAddress === 'function') {
                return ethersLib.utils.getAddress(address);
            }

            if (typeof ethersLib.getAddress === 'function') {
                return ethersLib.getAddress(address);
            }
        } catch (error) {
            console.warn('Không thể chuyển sang checksum address, dùng địa chỉ gốc.', error);
        }

        return address;
    }

    async function syncWithServer(options) {
        const { form, wallet, roleValue, action, transactionHash } = options;
        const tokenInput = form.querySelector('input[name="__RequestVerificationToken"]')
            || d.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenInput ? tokenInput.value : null;

        const response = await fetch('/admin/api/roles/sync', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                ...(token ? { 'RequestVerificationToken': token } : {})
            },
            body: JSON.stringify({
                walletAddress: wallet,
                role: roleValue,
                action,
                transactionHash
            })
        });

        const payload = await tryParseJson(response);
        if (!response.ok || !payload?.success) {
            throw new Error(payload?.error || 'Không thể đồng bộ trạng thái role.');
        }

        return payload;
    }

    function tryParseJson(response) {
        return response.text().then(text => {
            if (!text) {
                return {};
            }

            try {
                return JSON.parse(text);
            } catch (error) {
                console.warn('Không thể parse JSON từ phản hồi.', error);
                return {};
            }
        });
    }

    function setLoading(button, isLoading, label) {
        if (!button) {
            return;
        }

        if (isLoading) {
            if (!button.dataset.originalLabel) {
                button.dataset.originalLabel = button.innerHTML;
            }

            button.disabled = true;
            const content = label || 'Đang xử lý...';
            button.innerHTML = `<span class="spinner"></span> ${content}`;
            return;
        }

        button.disabled = false;
        if (button.dataset.originalLabel) {
            button.innerHTML = button.dataset.originalLabel;
            delete button.dataset.originalLabel;
        }
    }

    function toggleButtonBusy(button, isBusy, label) {
        setLoading(button, isBusy, label);
    }

    function showInlineError(element, message) {
        if (!element) {
            return;
        }

        element.textContent = message;
        element.hidden = false;
    }

    function clearInlineError(element) {
        if (!element) {
            return;
        }

        element.textContent = '';
        element.hidden = true;
    }

    function updateRoleModeHint(select) {
        if (!select) {
            return;
        }

        const hint = select.closest('.role-form')?.querySelector('.role-mode-hint');
        if (!hint) {
            return;
        }

        const option = select.options[select.selectedIndex];
        if (!option || !option.value) {
            hint.textContent = 'Chọn quyền để xem yêu cầu ký và phạm vi áp dụng.';
            hint.dataset.mode = '';
            return;
        }

        const requiresSignature = option.dataset.requiresSignature === 'true';
        const modeLabel = option.dataset.modeLabel || (requiresSignature ? 'On-chain' : 'Off-chain');
        const modeDescription = option.dataset.modeDescription || (requiresSignature
            ? 'Yêu cầu ký MetaMask và ghi nhận trên smart contract.'
            : 'Lưu trực tiếp trong cơ sở dữ liệu, không phát sinh giao dịch blockchain.');

        hint.textContent = `${modeLabel}: ${modeDescription}`;
        hint.dataset.mode = requiresSignature ? 'onchain' : 'offchain';
    }

    function getRoleMeta(select) {
        if (!select) {
            return { label: '', requiresSignature: false, modeLabel: '' };
        }

        const option = select.options[select.selectedIndex];
        if (!option) {
            return { label: '', requiresSignature: false, modeLabel: '' };
        }

        return {
            label: option.dataset.label || option.textContent.trim(),
            requiresSignature: option.dataset.requiresSignature === 'true',
            modeLabel: option.dataset.modeLabel || ''
        };
    }

    function getRoleLabel(select) {
        return getRoleMeta(select).label;
    }

    async function ensureWalletConnection() {
        const ethereum = await ensureEthereum();
        wireEthereumEvents(ethereum);

        let accounts = await ethereum.request({ method: 'eth_accounts' });
        if (!accounts || !accounts.length) {
            accounts = await ethereum.request({ method: 'eth_requestAccounts' });
        }

        if (!accounts || !accounts.length) {
            throw new Error('Không nhận được địa chỉ ví từ MetaMask.');
        }

        state.account = accounts[0];
        await ensureCorrectNetwork(ethereum);
        updateWalletPanel();
        return state.account;
    }

    async function ensureCorrectNetwork(ethereum) {
        let chainHex = await ethereum.request({ method: 'eth_chainId' });
        if (!chainHex) {
            return;
        }

        state.chainId = parseInt(chainHex, 16);
        state.networkLabel = formatNetworkLabel();

        if (!state.expectedChainHex) {
            return;
        }

        if (chainHex.toLowerCase() === state.expectedChainHex.toLowerCase()) {
            return;
        }

        try {
            await ethereum.request({
                method: 'wallet_switchEthereumChain',
                params: [{ chainId: state.expectedChainHex }]
            });
            chainHex = state.expectedChainHex;
        } catch (switchError) {
            if (switchError && (switchError.code === 4902 || switchError.code === '4902')) {
                const info = guessChainInfo(state.expectedChainDec, state.expectedChainHex);
                const params = {
                    chainId: state.expectedChainHex,
                    chainName: info.name || `Chain ${state.expectedChainDec ?? state.expectedChainHex}`,
                    nativeCurrency: {
                        name: info.nativeName || info.symbol || 'Native Token',
                        symbol: info.symbol || 'NATIVE',
                        decimals: info.decimals || 18
                    },
                    rpcUrls: state.rpcUrl ? [state.rpcUrl] : (info.rpcUrls || []).filter(Boolean),
                    blockExplorerUrls: info.explorer ? [info.explorer] : []
                };

                await ethereum.request({
                    method: 'wallet_addEthereumChain',
                    params: [params]
                });

                chainHex = state.expectedChainHex;
            } else {
                throw switchError;
            }
        }

        state.chainId = parseInt(chainHex, 16);
        state.networkLabel = formatNetworkLabel();
        updateWalletPanel();
    }

    function wireEthereumEvents(ethereum) {
        if (state.eventsBound || !ethereum) {
            return;
        }

        if (typeof ethereum.on === 'function') {
            ethereum.on('accountsChanged', accounts => {
                if (Array.isArray(accounts) && accounts.length) {
                    state.account = accounts[0];
                } else {
                    state.account = null;
                }

                updateWalletPanel();
            });

            ethereum.on('chainChanged', chainHex => {
                if (chainHex) {
                    state.chainId = parseInt(chainHex, 16);
                }

                state.networkLabel = formatNetworkLabel();
                updateWalletPanel();
            });
        }

        state.eventsBound = true;
    }

    async function ensureEthersReady() {
        if (w.ethers && w.ethers.Contract) {
            return w.ethers;
        }

        if (!state.ethersLoadingPromise) {
            state.ethersLoadingPromise = loadEthersFromCdns();
        }

        await state.ethersLoadingPromise;

        if (!w.ethers || !w.ethers.Contract) {
            throw new Error('Không thể tải thư viện ethers.');
        }

        return w.ethers;
    }

    async function loadEthersFromCdns() {
        if (w.ethers && w.ethers.Contract) {
            return;
        }

        for (const src of ethersCdns) {
            if (!src) {
                continue;
            }

            try {
                await injectScript(src);
                if (w.ethers && w.ethers.Contract) {
                    return;
                }
            } catch (error) {
                console.warn(`Không thể tải ethers từ ${src}`, error);
            }
        }
    }

    function injectScript(src) {
        return new Promise((resolve, reject) => {
            const existing = Array.from(d.scripts).some(s => s.src === src);
            if (existing) {
                resolve();
                return;
            }

            const script = d.createElement('script');
            script.src = src;
            script.async = true;
            script.onload = () => resolve();
            script.onerror = () => reject(new Error(`Không thể tải script: ${src}`));
            d.head.appendChild(script);
        });
    }

    async function waitForReceipt(tx) {
        if (!tx) {
            return null;
        }

        if (typeof tx.wait === 'function') {
            return tx.wait(1);
        }

        return tx;
    }

    function wrapMutationError(error) {
        if (!error) {
            return new Error('Không thể thực hiện giao dịch.');
        }

        if (error.code === 4001 || error.code === 'ACTION_REJECTED') {
            return new Error('Bạn đã từ chối giao dịch trong ví.');
        }

        return new Error(extractErrorMessage(error) || 'Không thể hoàn tất giao dịch.');
    }

    function normalizeErrorMessage(error) {
        if (!error) {
            return 'Đã xảy ra lỗi không xác định.';
        }

        if (typeof error === 'string') {
            return error;
        }

        return extractErrorMessage(error) || 'Đã xảy ra lỗi không xác định.';
    }

    function extractErrorMessage(error) {
        if (!error) {
            return '';
        }

        if (typeof error === 'string') {
            return error;
        }

        if (error.reason) {
            return error.reason;
        }

        if (error.message) {
            return error.message;
        }

        if (error.error) {
            return extractErrorMessage(error.error);
        }

        if (error.data) {
            return extractErrorMessage(error.data);
        }

        return '';
    }

    async function ensureEthereum() {
        const { ethereum } = w;
        if (!ethereum) {
            throw new Error('Không tìm thấy MetaMask hoặc ví tương thích.');
        }

        return ethereum;
    }

    async function createEthersProvider(ethersLib) {
        if (!w.ethereum) {
            throw new Error('Không tìm thấy MetaMask hoặc ví tương thích.');
        }

        if (typeof ethersLib?.BrowserProvider === 'function') {
            return new ethersLib.BrowserProvider(w.ethereum, 'any');
        }

        if (typeof ethersLib?.providers?.BrowserProvider === 'function') {
            return new ethersLib.providers.BrowserProvider(w.ethereum, 'any');
        }

        if (typeof ethersLib?.providers?.Web3Provider === 'function') {
            return new ethersLib.providers.Web3Provider(w.ethereum, 'any');
        }

        if (typeof ethersLib?.Web3Provider === 'function') {
            return new ethersLib.Web3Provider(w.ethereum, 'any');
        }

        throw new Error('Không thể khởi tạo provider từ thư viện ethers.');
    }

    async function getEthersSigner(provider) {
        if (!provider || typeof provider.getSigner !== 'function') {
            throw new Error('Provider không hỗ trợ ký giao dịch.');
        }

        const signer = provider.getSigner();
        if (signer && typeof signer.then === 'function') {
            return await signer;
        }

        return signer;
    }

    function getContractAbi() {
        const config = w.CONTRACT_CONFIG;
        if (config && Array.isArray(config.CONTRACT_ABI)) {
            return config.CONTRACT_ABI;
        }

        return null;
    }

    function guessChainInfo(chainIdDec, chainIdHex) {
        switch (chainIdDec) {
            case 56:
                return {
                    name: 'BNB Smart Chain',
                    symbol: 'BNB',
                    decimals: 18,
                    explorer: 'https://bscscan.com',
                    rpcUrls: ['https://bsc-dataseed.binance.org']
                };
            case 97:
                return {
                    name: 'BSC Testnet',
                    symbol: 'tBNB',
                    decimals: 18,
                    explorer: 'https://testnet.bscscan.com',
                    rpcUrls: ['https://data-seed-prebsc-1-s1.binance.org:8545']
                };
            default:
                return {
                    name: chainIdDec ? `Chain ${chainIdDec}` : (chainIdHex || 'Chain'),
                    symbol: 'ETH',
                    decimals: 18
                };
        }
    }

    function sanitizeHex(value) {
        if (!value) {
            return '';
        }

        const trimmed = value.trim();
        if (!trimmed) {
            return '';
        }

        return trimmed.startsWith('0x') ? trimmed : `0x${trimmed}`;
    }

    function toHex(value) {
        if (typeof value === 'number') {
            return `0x${value.toString(16)}`;
        }

        if (typeof value === 'bigint') {
            return `0x${value.toString(16)}`;
        }

        return value;
    }
})(window, document);
