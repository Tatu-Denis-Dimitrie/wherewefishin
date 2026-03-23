// Safety net: if Angular fails to bootstrap within 12 seconds and a Service Worker
// is registered (potentially serving stale/broken cached files), clear all SW caches
// and reload to recover automatically.
(function () {
  if (!('serviceWorker' in navigator)) return;
  window.__wwfReady = false;  // splash uses .sp class
  setTimeout(function () {
    if (window.__wwfReady) return;
    navigator.serviceWorker.getRegistrations().then(function (regs) {
      if (!regs.length) return;
      Promise.all(regs.map(function (r) { return r.unregister(); })).then(function () {
        caches.keys().then(function (keys) {
          Promise.all(keys.map(function (k) { return caches.delete(k); })).then(function () {
            window.location.reload();
          });
        });
      });
    });
  }, 12000);
})();
