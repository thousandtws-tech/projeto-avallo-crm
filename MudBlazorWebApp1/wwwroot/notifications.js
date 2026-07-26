window.nucleoNotifications = {
    isSupported: function () {
        return 'Notification' in window;
    },
    getPermission: function () {
        if (!('Notification' in window)) return 'unsupported';
        return Notification.permission;
    },
    requestPermission: async function () {
        if (!('Notification' in window)) return 'unsupported';
        try {
            const result = await Notification.requestPermission();
            return result;
        } catch (e) {
            console.error('Error requesting notification permission:', e);
            return 'denied';
        }
    },
    showNotification: function (title, body, icon, url) {
        if (!('Notification' in window) || Notification.permission !== 'granted') {
            return false;
        }
        const options = {
            body: body || '',
            icon: icon || '/favicon.png',
            badge: '/favicon.png',
            vibrate: [100, 50, 100],
            data: { url: url || '/notifications' }
        };

        if ('serviceWorker' in navigator && navigator.serviceWorker.controller) {
            navigator.serviceWorker.ready.then(function (registration) {
                registration.showNotification(title, options);
            }).catch(function () {
                const n = new Notification(title, options);
                n.onclick = function () {
                    window.focus();
                    if (options.data && options.data.url) {
                        window.location.href = options.data.url;
                    }
                };
            });
        } else {
            const n = new Notification(title, options);
            n.onclick = function () {
                window.focus();
                if (options.data && options.data.url) {
                    window.location.href = options.data.url;
                }
            };
        }
        return true;
    }
};
