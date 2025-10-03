(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        const userId = document.body?.dataset?.userId;
        if (!userId) {
            return;
        }

        const state = {
            userId,
            initialized: false,
            elements: [],
            connection: null,
            notifications: [],
            unreadCount: 0,
            lastError: null,
            isLoading: false,
            globalClickAttached: false,
            globalKeydownAttached: false,
            pendingReads: new Set()
        };

        setupNotificationCenters();
        // run again shortly in case topbar is rendered after partial updates
        setTimeout(setupNotificationCenters, 350);

        function setupNotificationCenters() {
            const topbars = document.querySelectorAll('.topbar');
            if (!topbars.length) {
                return;
            }

            let newElementsAdded = false;

            topbars.forEach(topbar => {
                if (topbar.dataset.notificationsInitialized === '1') {
                    return;
                }

                const element = buildNotificationAnchor(topbar);
                if (element) {
                    state.elements.push(element);
                    topbar.dataset.notificationsInitialized = '1';
                    newElementsAdded = true;
                }
            });

            if (!state.elements.length) {
                return;
            }

            if (newElementsAdded || !state.initialized) {
                renderAllNotificationWidgets();
            }

            if (!state.globalClickAttached) {
                document.addEventListener('click', closeAllNotificationDropdowns);
                state.globalClickAttached = true;
            }

            if (!state.globalKeydownAttached) {
                document.addEventListener('keydown', function (evt) {
                    if (evt.key === 'Escape') {
                        closeAllNotificationDropdowns();
                    }
                });
                state.globalKeydownAttached = true;
            }

            if (!state.initialized) {
                state.initialized = true;
                bootstrapNotificationData();
                startNotificationHub();
            }
        }

        function buildNotificationAnchor(topbar) {
            const wrapper = document.createElement('div');
            wrapper.className = 'notification-center';
            wrapper.innerHTML = `
                <button type="button" class="notification-bell" aria-haspopup="true" aria-expanded="false">
                    <ion-icon name="notifications-outline"></ion-icon>
                    <span class="notification-badge" aria-hidden="true">0</span>
                </button>
                <div class="notification-dropdown" role="menu" aria-label="Thông báo">
                    <div class="notification-header">
                        <span>Thông báo</span>
                        <button type="button" class="mark-all-read" disabled>Đánh dấu đã đọc</button>
                    </div>
                    <div class="notification-list">
                        <div class="notification-empty">Đang tải thông báo...</div>
                    </div>
                    <div class="notification-footer">
                        <a href="/User/Notifications">Xem tất cả</a>
                    </div>
                </div>`;

            const userNode = topbar.querySelector('.user');
            if (userNode) {
                topbar.insertBefore(wrapper, userNode);
            } else {
                topbar.appendChild(wrapper);
            }

            const bellButton = wrapper.querySelector('.notification-bell');
            const dropdown = wrapper.querySelector('.notification-dropdown');
            const list = wrapper.querySelector('.notification-list');
            const badge = wrapper.querySelector('.notification-badge');
            const markAll = wrapper.querySelector('.mark-all-read');

            const element = { wrapper, bellButton, dropdown, list, badge, markAll };

            if (bellButton) {
                bellButton.addEventListener('click', function (evt) {
                    evt.stopPropagation();
                    toggleNotificationDropdown(element);
                });
            }

            if (dropdown) {
                dropdown.addEventListener('click', function (evt) {
                    evt.stopPropagation();
                });
            }

            if (markAll) {
                markAll.addEventListener('click', function (evt) {
                    evt.preventDefault();
                    handleMarkAllNotificationsRead();
                });
            }

            return element;
        }

        function toggleNotificationDropdown(element) {
            if (!element || !element.dropdown) {
                return;
            }

            const isOpen = element.dropdown.classList.contains('open');
            closeAllNotificationDropdowns();
            if (!isOpen) {
                openNotificationDropdown(element);
            }
        }

        function openNotificationDropdown(element) {
            if (!element || !element.dropdown) {
                return;
            }

            element.dropdown.classList.add('open');
            element.wrapper?.classList.add('open');
            if (element.bellButton) {
                element.bellButton.setAttribute('aria-expanded', 'true');
            }
        }

        function closeNotificationDropdown(element) {
            if (!element || !element.dropdown) {
                return;
            }

            element.dropdown.classList.remove('open');
            element.wrapper?.classList.remove('open');
            if (element.bellButton) {
                element.bellButton.setAttribute('aria-expanded', 'false');
            }
        }

        function closeAllNotificationDropdowns() {
            state.elements.forEach(closeNotificationDropdown);
        }

        async function bootstrapNotificationData() {
            state.isLoading = true;
            state.lastError = null;
            renderAllNotificationWidgets();

            await Promise.allSettled([loadNotifications(), loadUnreadCount()]);

            state.isLoading = false;
            renderAllNotificationWidgets();
        }

        async function loadNotifications() {
            try {
                const response = await fetch('/User/Notifications', { credentials: 'same-origin' });
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const data = await response.json();
                if (Array.isArray(data)) {
                    state.notifications = data
                        .map(mapNotification)
                        .filter(Boolean)
                        .sort((a, b) => (b.createdAt?.getTime?.() ?? 0) - (a.createdAt?.getTime?.() ?? 0));
                } else {
                    state.notifications = [];
                }

                state.lastError = null;
            } catch (error) {
                console.warn('[Notifications] Failed to load notifications', error);
                state.lastError = 'Không thể tải thông báo.';
                state.notifications = state.notifications || [];
            }
        }

        async function loadUnreadCount() {
            try {
                const response = await fetch('/User/GetUnreadNotificationCount', { credentials: 'same-origin' });
                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                const data = await response.json();
                if (data && typeof data.count === 'number' && !Number.isNaN(data.count)) {
                    state.unreadCount = data.count;
                }
            } catch (error) {
                console.warn('[Notifications] Failed to load unread count', error);
            }
        }

        function startNotificationHub() {
            if (state.connection) {
                return;
            }

            if (typeof signalR === 'undefined') {
                console.warn('[Notifications] SignalR client script not found; realtime updates disabled.');
                return;
            }

            const connection = new signalR.HubConnectionBuilder()
                .withUrl('/notificationHub')
                .withAutomaticReconnect()
                .build();

            state.connection = connection;

            connection.on('NewNotification', onRealtimeNotification);
            connection.on('UnreadNotificationCountChanged', onUnreadNotificationCountChanged);

            connection.onreconnected(() => {
                connection.invoke('JoinUserGroup', state.userId).catch(err => {
                    console.warn('[Notifications] Failed to rejoin notification group', err);
                });
            });

            connection.onclose(() => {
                state.connection = null;
                setTimeout(() => {
                    if (!state.connection) {
                        startNotificationHub();
                    }
                }, 3000);
            });

            connection.start()
                .then(() => connection.invoke('JoinUserGroup', state.userId).catch(err => {
                    console.warn('[Notifications] Failed to subscribe to notification group', err);
                }))
                .catch(err => {
                    console.warn('[Notifications] Connection failed', err);
                    state.connection = null;
                    setTimeout(startNotificationHub, 5000);
                });
        }

        function onRealtimeNotification(payload) {
            const notification = mapNotification(payload);
            if (!notification) {
                return;
            }

            state.notifications = [notification]
                .concat(state.notifications.filter(item => item.id !== notification.id))
                .slice(0, 20);

            if (!notification.isRead) {
                state.unreadCount = (state.unreadCount || 0) + 1;
            }

            renderAllNotificationWidgets({ highlightFresh: true });
        }

        function onUnreadNotificationCountChanged(count) {
            if (typeof count !== 'number' || Number.isNaN(count)) {
                return;
            }

            state.unreadCount = count;
            renderAllNotificationWidgets();
        }

        function renderAllNotificationWidgets(options) {
            state.elements.forEach(element => renderNotificationWidget(element, options));
        }

        function renderNotificationWidget(element, options) {
            if (!element || !element.list) {
                return;
            }

            element.list.innerHTML = '';

            if (state.isLoading) {
                element.list.appendChild(createInfoRow('Đang tải thông báo...', 'notification-empty'));
            } else if (state.lastError) {
                element.list.appendChild(createInfoRow(state.lastError, 'notification-error'));
            } else if (!state.notifications.length) {
                element.list.appendChild(createInfoRow('Không có thông báo.', 'notification-empty'));
            } else {
                const items = state.notifications.slice(0, 15);
                items.forEach((notification, index) => {
                    const item = createNotificationItem(notification);
                    if (options && options.highlightFresh && index === 0) {
                        item.classList.add('just-arrived');
                        setTimeout(() => item.classList.remove('just-arrived'), 1200);
                    }
                    element.list.appendChild(item);
                });
            }

            updateBadge(element);
            updateMarkAllButton(element);
        }

        function createInfoRow(text, className) {
            const div = document.createElement('div');
            div.className = className;
            div.textContent = text;
            return div;
        }

        function updateBadge(element) {
            if (!element || !element.badge) {
                return;
            }

            const count = Number(state.unreadCount) || 0;
            if (count > 0) {
                element.badge.textContent = count > 99 ? '99+' : count.toString();
                element.badge.classList.add('is-visible');
                element.wrapper?.classList.add('has-unread');
            } else {
                element.badge.classList.remove('is-visible');
                element.wrapper?.classList.remove('has-unread');
            }
        }

        function updateMarkAllButton(element) {
            if (!element || !element.markAll) {
                return;
            }

            const hasUnread = state.notifications.some(notification => !notification.isRead);
            element.markAll.disabled = !hasUnread;
        }

        function createNotificationItem(notification) {
            const item = document.createElement('button');
            item.type = 'button';
            item.className = 'notification-item';
            if (!notification.isRead) {
                item.classList.add('unread');
            }

            const visuals = resolveNotificationVisual(notification.type);
            if (visuals.tone) {
                item.classList.add(visuals.tone);
            }

            const icon = document.createElement('div');
            icon.className = 'notification-icon';
            const iconElement = document.createElement('ion-icon');
            iconElement.setAttribute('name', visuals.icon);
            icon.appendChild(iconElement);

            const content = document.createElement('div');
            content.className = 'notification-content';

            const title = document.createElement('div');
            title.className = 'notification-title';
            title.textContent = notification.title || 'Thông báo';
            content.appendChild(title);

            if (notification.message) {
                const message = document.createElement('div');
                message.className = 'notification-message';
                message.textContent = notification.message;
                content.appendChild(message);
            }

            const metaFragments = [];
            const relativeTime = formatRelativeTime(notification.createdAt || notification.createdAtRaw);
            if (relativeTime) {
                metaFragments.push(relativeTime);
            }
            const typeLabel = formatNotificationTypeLabel(notification.type);
            if (typeLabel) {
                metaFragments.push(typeLabel);
            }

            if (metaFragments.length) {
                const meta = document.createElement('div');
                meta.className = 'notification-meta';
                meta.textContent = metaFragments.join(' • ');
                content.appendChild(meta);
            }

            item.appendChild(icon);
            item.appendChild(content);

            item.addEventListener('click', function () {
                closeAllNotificationDropdowns();
                handleNotificationClick(notification);
            });

            return item;
        }

        async function handleNotificationClick(notification) {
            if (!notification) {
                return;
            }

            const wasUnread = !notification.isRead;
            if (wasUnread) {
                const success = await markNotificationAsRead(notification.id);
                if (success) {
                    notification.isRead = true;
                    state.unreadCount = Math.max(0, state.unreadCount - 1);
                    renderAllNotificationWidgets();
                }
            }

            const payload = parseNotificationData(notification.data);
            if (payload && payload.url) {
                if (payload.openInNewTab) {
                    window.open(payload.url, '_blank', 'noopener');
                } else {
                    window.location.href = payload.url;
                }
            }
        }

        async function markNotificationAsRead(notificationId) {
            if (!notificationId || state.pendingReads.has(notificationId)) {
                return true;
            }

            state.pendingReads.add(notificationId);

            try {
                const response = await fetch('/User/MarkNotificationRead', {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ notificationId })
                });

                if (!response.ok) {
                    throw new Error(`HTTP ${response.status}`);
                }

                return true;
            } catch (error) {
                console.warn('[Notifications] Failed to mark notification as read', error);
                return false;
            } finally {
                state.pendingReads.delete(notificationId);
            }
        }

        async function handleMarkAllNotificationsRead() {
            const unread = state.notifications.filter(notification => !notification.isRead);
            if (!unread.length) {
                closeAllNotificationDropdowns();
                return;
            }

            const ids = unread.map(notification => notification.id).filter(Boolean);
            if (!ids.length) {
                closeAllNotificationDropdowns();
                return;
            }

            await Promise.allSettled(ids.map(id => markNotificationAsRead(id)));

            unread.forEach(notification => {
                notification.isRead = true;
            });
            state.unreadCount = 0;
            renderAllNotificationWidgets();
            closeAllNotificationDropdowns();
        }

        function mapNotification(raw) {
            if (!raw) {
                return null;
            }

            const id = raw.id ?? raw.ID ?? 0;
            const title = raw.title ?? raw.Title ?? 'Thông báo';
            const message = raw.message ?? raw.Message ?? '';
            const type = raw.type ?? raw.Type ?? '';
            const data = raw.data ?? raw.Data ?? null;
            const createdRaw = raw.createdAt ?? raw.CreatedAt ?? null;
            const createdAt = createdRaw ? new Date(createdRaw) : null;
            const isRead = Boolean(raw.isRead ?? raw.IsRead);

            return {
                id,
                title,
                message,
                type,
                data,
                createdAt,
                createdAtRaw: createdRaw,
                isRead
            };
        }

        function parseNotificationData(raw) {
            if (!raw) {
                return null;
            }

            if (typeof raw === 'object') {
                return raw;
            }

            if (typeof raw === 'string') {
                try {
                    return JSON.parse(raw);
                } catch (error) {
                    return { message: raw };
                }
            }

            return null;
        }

        function resolveNotificationVisual(type) {
            const key = (type || '').toString().toLowerCase();

            if (!key) {
                return { tone: '', icon: 'notifications-outline' };
            }

            if (key.includes('risk') || key.includes('alert') || key.includes('warning')) {
                return { tone: 'warning', icon: 'alert-circle-outline' };
            }

            if (key.includes('error') || key.includes('fail') || key.includes('withdraw')) {
                return { tone: 'danger', icon: 'warning-outline' };
            }

            if (key.includes('success') || key.includes('approve') || key.includes('complete')) {
                return { tone: 'success', icon: 'checkmark-circle-outline' };
            }

            if (key.includes('kyc') || key.includes('compliance')) {
                return { tone: '', icon: 'shield-checkmark-outline' };
            }

            if (key.includes('message') || key.includes('chat')) {
                return { tone: '', icon: 'chatbubble-ellipses-outline' };
            }

            return { tone: '', icon: 'notifications-outline' };
        }

        function formatNotificationTypeLabel(type) {
            if (!type) {
                return '';
            }

            const label = type.toString();
            const lower = label.toLowerCase();

            if (lower.includes('risk')) {
                return 'Cảnh báo';
            }

            if (lower.includes('kyc')) {
                return 'KYC';
            }

            if (lower.includes('compliance')) {
                return 'Tuân thủ';
            }

            if (lower.includes('campaign')) {
                return 'Chiến dịch';
            }

            if (lower.includes('withdraw')) {
                return 'Rút vốn';
            }

            if (lower.includes('profile')) {
                return 'Hồ sơ';
            }

            if (lower.includes('system')) {
                return 'Hệ thống';
            }

            return label;
        }

        function formatRelativeTime(value) {
            if (!value) {
                return '';
            }

            const date = value instanceof Date ? value : new Date(value);
            const timestamp = date.getTime();
            if (Number.isNaN(timestamp)) {
                return '';
            }

            const diffSeconds = Math.floor((Date.now() - timestamp) / 1000);
            if (diffSeconds < 60) {
                return `${diffSeconds}s trước`;
            }

            const diffMinutes = Math.floor(diffSeconds / 60);
            if (diffMinutes < 60) {
                return `${diffMinutes} phút trước`;
            }

            const diffHours = Math.floor(diffMinutes / 60);
            if (diffHours < 24) {
                return `${diffHours} giờ trước`;
            }

            const diffDays = Math.floor(diffHours / 24);
            if (diffDays < 7) {
                return `${diffDays} ngày trước`;
            }

            return date.toLocaleString('vi-VN', {
                day: '2-digit',
                month: '2-digit',
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit'
            });
        }
    });
})();
