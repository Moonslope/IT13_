(function () {
    const ensureElements = () => {
        let backdrop = document.getElementById('app-alert-backdrop');
        if (!backdrop) {
            backdrop = document.createElement('div');
            backdrop.id = 'app-alert-backdrop';
            backdrop.className = 'app-alert-backdrop';
            document.body.appendChild(backdrop);
        }

        let modal = document.getElementById('app-alert-modal');
        if (!modal) {
            modal = document.createElement('div');
            modal.id = 'app-alert-modal';
            modal.className = 'app-alert-modal';
            modal.innerHTML = `
                <div class="app-alert-header">
                    <div class="app-alert-title">HestiaLink</div>
                    <button class="app-alert-close" aria-label="Close">×</button>
                </div>
                <div class="app-alert-body"></div>
                <div class="app-alert-footer">
                    <button class="app-alert-button">OK</button>
                </div>
            `;
            document.body.appendChild(modal);

            const close = () => {
                backdrop.classList.remove('show');
                modal.classList.remove('show');
            };

            modal.querySelector('.app-alert-close').addEventListener('click', close);
            modal.querySelector('.app-alert-button').addEventListener('click', close);
            backdrop.addEventListener('click', close);
        }

        return { backdrop, modal };
    };

    const show = (message, title) => {
        const { backdrop, modal } = ensureElements();
        const body = modal.querySelector('.app-alert-body');
        const titleEl = modal.querySelector('.app-alert-title');
        titleEl.textContent = title || 'Notice';
        body.textContent = message || '';

        backdrop.classList.add('show');
        modal.classList.add('show');
    };

    window.showAppAlert = show;

    // Override native alert to maintain compatibility
    window.alert = function (msg) {
        show(msg, 'Notice');
    };
})();
