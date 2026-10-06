// Admin: the auth cookie answers fetch() with 401/403 instead of redirecting.
// Stop the caller (reject) so modals never render the login / access-denied page.
(() => {
  const nativeFetch = window.fetch.bind(window);
  window.fetch = async (...args) => {
    const res = await nativeFetch(...args);
    if (res.status === 401) {
      location.href = '/admin/login?ReturnUrl=' + encodeURIComponent(location.pathname + location.search);
      throw new Error('Unauthorized');
    }
    if (res.status === 403) {
      alert('Bạn không có quyền thực hiện thao tác này.');
      throw new Error('Forbidden');
    }
    return res;
  };
})();
