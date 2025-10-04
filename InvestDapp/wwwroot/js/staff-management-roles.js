// ===== STAFF MANAGEMENT ROLES - METAMASK INTEGRATION =====
// Handles on-chain role signing (SuperAdmin, Admin) with MetaMask

// Multiple CDN fallbacks for ethers.js
const ETHERS_CDN_URLS = [
    'https://cdn.ethers.io/lib/ethers-5.7.2.umd.min.js',
    'https://cdnjs.cloudflare.com/ajax/libs/ethers/5.7.2/ethers.umd.min.js',
    'https://unpkg.com/ethers@5.7.2/dist/ethers.umd.min.js'
];

let ethersLoaded = false;

// Load ethers.js from CDN with fallbacks
async function loadEthers() {
    if (ethersLoaded || window.ethers) {
        ethersLoaded = true;
        return true;
    }

    for (const url of ETHERS_CDN_URLS) {
        try {
            await new Promise((resolve, reject) => {
                const script = document.createElement('script');
                script.src = url;
                script.onload = resolve;
                script.onerror = reject;
                document.head.appendChild(script);
            });

            if (window.ethers) {
                console.log('✅ Ethers.js loaded from:', url);
                ethersLoaded = true;
                return true;
            }
        } catch (err) {
            console.warn(`⚠️ Failed to load ethers.js from ${url}, trying next...`);
        }
    }

    throw new Error('❌ Could not load ethers.js from any CDN');
}

// Connect MetaMask wallet
async function connectMetaMask() {
    if (!window.ethereum) {
        throw new Error('❌ MetaMask chưa được cài đặt! Vui lòng cài đặt MetaMask Extension.');
    }

    try {
        const accounts = await window.ethereum.request({ 
            method: 'eth_requestAccounts' 
        });
        
        if (!accounts || accounts.length === 0) {
            throw new Error('❌ Không có tài khoản MetaMask nào được kết nối.');
        }

        console.log('✅ MetaMask connected:', accounts[0]);
        return accounts[0];
    } catch (error) {
        console.error('❌ MetaMask connection failed:', error);
        throw new Error('Kết nối MetaMask thất bại: ' + error.message);
    }
}

// Get contract instance
function getContract(signer) {
    if (!window.CONTRACT_CONFIG) {
        throw new Error('❌ CONTRACT_CONFIG không tồn tại! Kiểm tra file contract-config.js');
    }

    const { CONTRACT_ADDRESS, CONTRACT_ABI } = window.CONTRACT_CONFIG;
    
    if (!CONTRACT_ADDRESS || !CONTRACT_ABI) {
        throw new Error('❌ Thiếu CONTRACT_ADDRESS hoặc CONTRACT_ABI trong contract-config.js');
    }

    return new ethers.Contract(CONTRACT_ADDRESS, CONTRACT_ABI, signer);
}

// Grant role on-chain via smart contract
async function grantRoleOnChain(walletAddress, roleValue) {
    console.log('🔗 Starting on-chain role grant:', { walletAddress, roleValue });

    // Load ethers.js if not loaded
    await loadEthers();

    // Connect MetaMask
    const connectedAccount = await connectMetaMask();
    console.log('✅ Connected account:', connectedAccount);

    // Create provider and signer
    const provider = new ethers.providers.Web3Provider(window.ethereum);
    const signer = provider.getSigner();

    // Get contract instance
    const contract = getContract(signer);
    console.log('✅ Contract instance created:', contract.address);

    // Call appropriate contract method
    let tx;
    if (roleValue === 'Admin') {
        console.log('📝 Calling grantAdmin...');
        tx = await contract.grantAdmin(walletAddress);
    } else if (roleValue === 'SuperAdmin') {
        console.log('📝 Calling grantRole with DEFAULT_ADMIN_ROLE...');
        const DEFAULT_ADMIN_ROLE = '0x0000000000000000000000000000000000000000000000000000000000000000';
        tx = await contract.grantRole(DEFAULT_ADMIN_ROLE, walletAddress);
    } else {
        throw new Error('❌ Invalid on-chain role: ' + roleValue);
    }

    console.log('⏳ Transaction submitted:', tx.hash);
    console.log('⏳ Waiting for confirmation...');

    // Wait for transaction confirmation
    const receipt = await tx.wait();
    console.log('✅ Transaction confirmed:', receipt);

    return {
        hash: tx.hash,
        blockNumber: receipt.blockNumber,
        status: receipt.status === 1 ? 'success' : 'failed'
    };
}

// Revoke role on-chain via smart contract
async function revokeRoleOnChain(walletAddress, roleValue) {
    console.log('🔗 Starting on-chain role revoke:', { walletAddress, roleValue });

    // Load ethers.js if not loaded
    await loadEthers();

    // Connect MetaMask
    const connectedAccount = await connectMetaMask();
    console.log('✅ Connected account:', connectedAccount);

    // Create provider and signer
    const provider = new ethers.providers.Web3Provider(window.ethereum);
    const signer = provider.getSigner();

    // Get contract instance
    const contract = getContract(signer);
    console.log('✅ Contract instance created:', contract.address);

    // Call appropriate contract method
    let tx;
    if (roleValue === 'Admin') {
        console.log('📝 Calling revokeAdmin...');
        tx = await contract.revokeAdmin(walletAddress);
    } else if (roleValue === 'SuperAdmin') {
        console.log('📝 Calling revokeRole with DEFAULT_ADMIN_ROLE...');
        const DEFAULT_ADMIN_ROLE = '0x0000000000000000000000000000000000000000000000000000000000000000';
        tx = await contract.revokeRole(DEFAULT_ADMIN_ROLE, walletAddress);
    } else {
        throw new Error('❌ Invalid on-chain role: ' + roleValue);
    }

    console.log('⏳ Transaction submitted:', tx.hash);
    console.log('⏳ Waiting for confirmation...');

    // Wait for transaction confirmation
    const receipt = await tx.wait();
    console.log('✅ Transaction confirmed:', receipt);

    return {
        hash: tx.hash,
        blockNumber: receipt.blockNumber,
        status: receipt.status === 1 ? 'success' : 'failed'
    };
}

// Show loading overlay
function showLoading(message) {
    // Remove existing overlay if any
    hideLoading();
    
    const overlay = document.createElement('div');
    overlay.className = 'loading-overlay';
    overlay.innerHTML = `
        <div class="loading-content">
            <div class="spinner"></div>
            <h3>Đang xử lý Blockchain</h3>
            <p>${message || 'Vui lòng đợi...'}</p>
        </div>
    `;
    document.body.appendChild(overlay);
}

// Hide loading overlay
function hideLoading() {
    const overlay = document.querySelector('.loading-overlay');
    if (overlay) {
        overlay.remove();
    }
}

// Intercept form submissions for on-chain roles
document.addEventListener('DOMContentLoaded', function() {
    const forms = document.querySelectorAll('.staff-role-form');

    forms.forEach(form => {
        form.addEventListener('submit', async function(e) {
            const roleAction = this.getAttribute('data-role-action'); // 'grant' or 'revoke'
            const selectedRoleInput = this.querySelector('input[name="role"]:checked');

            if (!selectedRoleInput) {
                return; // No role selected, let validation handle it
            }

            const requiresSignature = selectedRoleInput.getAttribute('data-requires-signature') === 'true';
            const roleValue = selectedRoleInput.value;
            const roleLabel = selectedRoleInput.getAttribute('data-role-label');
            const modeLabel = selectedRoleInput.getAttribute('data-mode-label');

            console.log('📋 Form submission:', { roleAction, roleValue, roleLabel, modeLabel, requiresSignature });

            // Only intercept on-chain roles
            if (!requiresSignature) {
                console.log('✅ Off-chain role - submitting normally');
                return; // Let form submit normally for off-chain roles
            }

            // Prevent default submission for on-chain roles
            e.preventDefault();

            const walletAddressInput = this.querySelector('input[name="walletAddress"]');
            const walletAddress = walletAddressInput ? walletAddressInput.value : null;

            if (!walletAddress) {
                alert('❌ Lỗi: Không tìm thấy địa chỉ ví của nhân viên này.');
                return;
            }

            try {
                showLoading(roleAction === 'grant' 
                    ? `Đang cấp quyền ${roleLabel} (${modeLabel}) cho ví ${walletAddress.substring(0, 10)}...`
                    : `Đang thu hồi quyền ${roleLabel} (${modeLabel}) từ ví ${walletAddress.substring(0, 10)}...`
                );

                let result;
                if (roleAction === 'grant') {
                    result = await grantRoleOnChain(walletAddress, roleValue);
                } else if (roleAction === 'revoke') {
                    result = await revokeRoleOnChain(walletAddress, roleValue);
                } else {
                    throw new Error('❌ Invalid role action: ' + roleAction);
                }

                console.log('✅ Blockchain transaction successful:', result);

                hideLoading();

                // Set hidden fields to signal successful blockchain transaction
                const alreadySignedInput = this.querySelector('input[name="alreadySigned"]');
                const transactionHashInput = this.querySelector('input[name="transactionHash"]');

                if (alreadySignedInput) alreadySignedInput.value = 'true';
                if (transactionHashInput) transactionHashInput.value = result.hash;

                // Submit form normally to update database
                console.log('📤 Submitting form to update database...');
                this.submit();

            } catch (error) {
                hideLoading();
                console.error('❌ Blockchain transaction failed:', error);
                alert('❌ Giao dịch Blockchain thất bại:\n\n' + error.message);
            }
        });
    });
});

console.log('✅ Staff Management Roles MetaMask Integration loaded');
