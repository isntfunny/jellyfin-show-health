export default function (view) {
    'use strict';

    view.addEventListener('viewshow', function () {
        var statusEl = view.querySelector('#showHealthStatus');
        statusEl.textContent = 'Hello World — Show Health plugin is loaded.';
    });
}
