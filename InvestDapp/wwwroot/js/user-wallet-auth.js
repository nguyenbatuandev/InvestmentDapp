/**
 * ===================================================================
 * USER WALLET AUTHENTICATION WITH METAMASK SIGNATURE
 * ===================================================================
 * Implements secure wallet authentication flow:
 * 1. Connect MetaMask
 * 2. Generate nonce from server
 * 3. Sign nonce with MetaMask (personal_sign)
 * 4. Verify signature on server
 * 5. Auto sign-in user
 * 
 * This replaces the demo login flow with proper cryptographic verification.
 * ===================================================================
 */

(function() {
    'use strict';

    const EXPECTED_CHAIN_ID = '0x61'; // BSC Testnet
    const EXPECTED_CHAIN_ID_DEC = 97;

    // API endpoints
    const API = {
        nonce: '/Wallet/Nonce',
        verify: '/Wallet/Verify',
        saveProfile: '/Wallet/SaveProfile',
        logout: '/Wallet/Logout'
    };

    // State
    let currentAccount = null;
    let cachedNonce = null;
    let isAuthenticating = false;

    /**
     * Check if MetaMask is available
     */
    function requireMetaMask() {
        if (!window.ethereum) {
            throw new Error('⚠️ Cần cài đặt MetaMask Extension để kết nối ví.');
        }
        return window.ethereum;
    }

    /**
     * Ensure BSC Testnet is selected
     */
    async function ensureBscTestnet() {
        const ethereum = requireMetaMask();
        
        try {
            const chainId = await ethereum.request({ method: 'eth_chainId' });
            
            if (chainId === EXPECTED_CHAIN_ID) {
                return true;
            }

            // Try to switch network
            try {
                await ethereum.request({
                    method: 'wallet_switchEthereumChain',
                    params: [{ chainId: EXPECTED_CHAIN_ID }]
                });
                return true;
            } catch (switchError) {
                // Chain hasn't been added to MetaMask yet
                if (switchError.code === 4902 || switchError.message?.includes('Unrecognized chain')) {
                    await ethereum.request({
                        method: 'wallet_addEthereumChain',
                        params: [{
                            chainId: EXPECTED_CHAIN_ID,
                            chainName: 'BSC Testnet',
                            nativeCurrency: {
                                name: 'Binance Coin',
                                symbol: 'tBNB',
                                decimals: 18
                            },
                            rpcUrls: [
                                'https://data-seed-prebsc-1-s1.binance.org:8545/',
                                'https://data-seed-prebsc-2-s1.binance.org:8545/'
                            ],
                            blockExplorerUrls: ['https://testnet.bscscan.com']
                        }]
                    });
                    
                    // Try switching again after adding
                    await ethereum.request({
                        method: 'wallet_switchEthereumChain',
                        params: [{ chainId: EXPECTED_CHAIN_ID }]
                    });
                    
                    return true;
                }
                
                console.warn('Failed to switch to BSC Testnet', switchError);
                return false;
            }
        } catch (error) {
            console.error('Network check failed:', error);
            return false;
        }
    }

    /**
     * Request nonce from server
     */
    async function fetchNonce(walletAddress) {
        const response = await fetch(API.nonce, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ walletAddress })
        });

        const data = await response.json();
        
        if (!response.ok || !data.success) {
            throw new Error(data.error || 'Không thể lấy nonce từ server.');
        }

        return data.nonce;
    }

    /**
     * Sign nonce with MetaMask
     */
    async function signNonce(walletAddress, nonce) {
        const ethereum = requireMetaMask();
        
        // Ensure wallet address is properly formatted (checksummed)
        if (!walletAddress || !walletAddress.startsWith('0x') || walletAddress.length !== 42) {
            console.error('❌ Invalid wallet address for signing:', walletAddress);
            throw new Error('Địa chỉ ví không hợp lệ.');
        }
        
        if (!nonce || typeof nonce !== 'string') {
            console.error('❌ Invalid nonce for signing:', nonce);
            throw new Error('Nonce không hợp lệ.');
        }
        
        console.log('🔐 Signing with wallet:', walletAddress);
        console.log('🔐 Nonce to sign:', nonce.substring(0, 50) + '...');
        
        // MetaMask personal_sign expects: [message, address]
        // message should be hex-encoded or plain string
        try {
            const signature = await ethereum.request({
                method: 'personal_sign',
                params: [nonce, walletAddress.toLowerCase()]
            });
            
            console.log('✅ Signature received:', signature.substring(0, 20) + '...');
            return signature;
        } catch (error) {
            console.error('❌ MetaMask signing error:', error);
            throw new Error('Người dùng từ chối ký hoặc có lỗi: ' + error.message);
        }
    }

    /**
     * Verify signature on server and sign in
     */
    async function verifySignature(walletAddress, signature) {
        const response = await fetch(API.verify, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ walletAddress, signature })
        });

        const data = await response.json();
        
        if (!response.ok || !data.success) {
            throw new Error(data.error || 'Xác thực thất bại.');
        }

        return data;
    }

    /**
     * Main authentication flow
     */
    async function authenticateWallet() {
        if (isAuthenticating) {
            console.log('Authentication already in progress...');
            return null;
        }

        isAuthenticating = true;

        try {
            const ethereum = requireMetaMask();

            // Step 1: Ensure correct network
            const networkOk = await ensureBscTestnet();
            if (!networkOk) {
                throw new Error('Vui lòng chuyển mạng sang BSC Testnet trong MetaMask.');
            }

            // Step 2: Request MetaMask accounts
            const accounts = await ethereum.request({ method: 'eth_requestAccounts' });
            if (!accounts || !accounts.length) {
                throw new Error('Không nhận được địa chỉ ví từ MetaMask.');
            }

            // Validate and normalize account address
            const account = accounts[0];
            if (!account || typeof account !== 'string' || !account.startsWith('0x') || account.length !== 42) {
                throw new Error('Địa chỉ ví từ MetaMask không hợp lệ.');
            }

            currentAccount = account.toLowerCase();
            console.log('✅ Wallet connected:', currentAccount);

            // Step 3: Generate nonce
            console.log('🔑 Generating nonce...');
            cachedNonce = await fetchNonce(currentAccount);
            if (!cachedNonce) {
                throw new Error('Không nhận được nonce từ server.');
            }
            console.log('✅ Nonce received');

            // Step 4: Request signature
            console.log('✍️ Requesting signature...');
            const signature = await signNonce(currentAccount, cachedNonce);
            if (!signature) {
                throw new Error('Không nhận được chữ ký từ MetaMask.');
            }
            console.log('✅ Signature obtained');

            // Step 5: Verify signature and sign in
            console.log('🔐 Verifying signature...');
            const result = await verifySignature(currentAccount, signature);
            console.log('✅ Authentication successful!');

            // Clear cached nonce
            cachedNonce = null;

            // Save to localStorage
            localStorage.setItem('connectedWallet', currentAccount);

            return {
                success: true,
                account: currentAccount,
                requiresProfile: result.requiresProfile,
                profile: result.profile
            };

        } catch (error) {
            console.error('❌ Authentication failed:', error);
            currentAccount = null;
            cachedNonce = null;
            throw error;
        } finally {
            isAuthenticating = false;
        }
    }

    /**
     * Save user profile
     */
    async function saveProfile(wallet, name, email) {
        const antiforgeryToken = getRequestVerificationToken();
        
        const response = await fetch(API.saveProfile, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': antiforgeryToken
            },
            body: new URLSearchParams({ wallet, name, email })
        });

        const data = await response.json();
        return data.success;
    }

    /**
     * Logout
     */
    async function logout() {
        const antiforgeryToken = getRequestVerificationToken();
        
        try {
            await fetch(API.logout, {
                method: 'POST',
                headers: {
                    'RequestVerificationToken': antiforgeryToken
                }
            });
        } catch (error) {
            console.warn('Logout request failed:', error);
        }

        localStorage.removeItem('connectedWallet');
        currentAccount = null;
        cachedNonce = null;
    }

    /**
     * Get anti-forgery token
     */
    function getRequestVerificationToken() {
        // Try window function first
        if (typeof window.getRequestVerificationToken === 'function') {
            return window.getRequestVerificationToken();
        }
        
        // Fallback to form input
        const input = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    /**
     * Setup MetaMask event listeners
     */
    function setupMetaMaskListeners() {
        if (!window.ethereum) return;

        // Account changed
        window.ethereum.on('accountsChanged', async (accounts) => {
            if (!accounts || accounts.length === 0) {
                console.log('MetaMask disconnected');
                await logout();
                
                // Trigger UI update
                if (typeof window.handleWalletDisconnected === 'function') {
                    window.handleWalletDisconnected();
                }
            } else if (accounts[0] !== currentAccount) {
                console.log('MetaMask account changed, need re-authentication');
                await logout();
                
                // Trigger UI update
                if (typeof window.handleWalletDisconnected === 'function') {
                    window.handleWalletDisconnected();
                }
            }
        });

        // Chain changed
        window.ethereum.on('chainChanged', async (chainId) => {
            const chainIdDec = parseInt(chainId, 16);
            if (chainIdDec !== EXPECTED_CHAIN_ID_DEC) {
                console.warn('⚠️ Wrong network! Please switch to BSC Testnet.');
                
                // Trigger warning
                if (typeof window.handleWrongNetwork === 'function') {
                    window.handleWrongNetwork();
                }
            }
        });

        // Disconnect
        window.ethereum.on('disconnect', async () => {
            console.log('MetaMask disconnected');
            await logout();
            
            // Trigger UI update
            if (typeof window.handleWalletDisconnected === 'function') {
                window.handleWalletDisconnected();
            }
        });
    }

    /**
     * Check if already connected (for auto-reconnect)
     */
    async function checkExistingConnection() {
        if (!window.ethereum) return null;

        try {
            const accounts = await window.ethereum.request({ method: 'eth_accounts' });
            const savedWallet = localStorage.getItem('connectedWallet');
            
            if (accounts && accounts.length > 0 && savedWallet) {
                const currentAddr = accounts[0].toLowerCase();
                const savedAddr = savedWallet.toLowerCase();
                
                if (currentAddr === savedAddr) {
                    currentAccount = accounts[0];
                    return currentAccount;
                }
            }
        } catch (error) {
            console.warn('Failed to check existing connection:', error);
        }

        return null;
    }

    // ==========================================
    // PUBLIC API
    // ==========================================

    window.UserWalletAuth = {
        authenticate: authenticateWallet,
        logout: logout,
        saveProfile: saveProfile,
        checkExistingConnection: checkExistingConnection,
        getCurrentAccount: () => currentAccount,
        isAuthenticating: () => isAuthenticating
    };

    // Setup listeners on load
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', setupMetaMaskListeners);
    } else {
        setupMetaMaskListeners();
    }

    console.log('✅ User Wallet Authentication module loaded');

})();
