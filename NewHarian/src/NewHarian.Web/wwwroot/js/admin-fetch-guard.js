// Admin: the auth cookie answers fetch() with 401/403 instead of redirecting.
// Stop the caller (reject) so modals never render the login / access-denied page.
// Server errors and network failures also reject, with an error toast. Background polling opts out of
// toasts with the X-Admin-Silent header.
(() => {
  const nativeFetch = window.fetch.bind(window);

  function isSilent(init) {
    const h = init?.headers;
    if (!h) return false;
    if (h instanceof Headers) return h.has('X-Admin-Silent');
    return Object.keys(h).some(k => k.toLowerCase() === 'x-admin-silent');
  }

  function toastError(message) {
    if (window.adminToast) window.adminToast.error(message);
    else alert(message);
  }

  window.fetch = async (...args) => {
    const silent = isSilent(args[1]);
    let res;
    try {
      res = await nativeFetch(...args);
    } catch (err) {
      if (!silent) toastError('Không kết nối được máy chủ. Vui lòng kiểm tra mạng và thử lại.');
      throw err;
    }
    if (res.status === 401) {
      location.href = '/admin/login?ReturnUrl=' + encodeURIComponent(location.pathname + location.search);
      throw new Error('Unauthorized');
    }
    if (res.status === 403) {
      if (!silent) toastError('Bạn không có quyền thực hiện thao tác này.');
      throw new Error('Forbidden');
    }
    if (res.status >= 500) {
      if (!silent) toastError('Lỗi hệ thống, vui lòng thử lại sau.');
      throw new Error('Server error ' + res.status);
    }
    return res;
  };
})();
