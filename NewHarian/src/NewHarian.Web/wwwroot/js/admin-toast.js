// Admin success/failure toasts. Server-rendered toasts come from _AdminToasts (TempData / invalid POST);
// fetch flows call adminToast.* directly, or adminToast.reloadWith() to show the message after navigation.
(() => {
  const FLASH_KEY = 'adminToast.flash';
  const SUCCESS_MS = 4000;
  const DEFAULT_ERROR = 'Không thực hiện được. Vui lòng thử lại.';
  const INVALID_FORM = 'Không lưu được. Vui lòng kiểm tra các trường báo lỗi.';

  // An open <dialog> sits in the top layer, so toasts must live inside it to be visible.
  function host() {
    const dialogs = document.querySelectorAll('dialog[open]');
    const parent = dialogs.length ? dialogs[dialogs.length - 1] : document.body;
    let el = parent.querySelector(':scope > .admin-toasts');
    if (!el) {
      el = document.createElement('div');
      el.className = 'admin-toasts';
      el.setAttribute('aria-live', 'polite');
      parent.appendChild(el);
    }
    return el;
  }

  function dismiss(toast) {
    toast.classList.add('is-leaving');
    setTimeout(() => toast.remove(), 200);
  }

  function arm(toast) {
    toast.querySelector('.admin-toast__close')?.addEventListener('click', () => dismiss(toast));
    if (toast.classList.contains('admin-toast--success')) setTimeout(() => dismiss(toast), SUCCESS_MS);
  }

  function show(kind, message) {
    const isError = kind === 'error';
    const text = (message || '').toString().trim() || (isError ? DEFAULT_ERROR : 'Đã lưu.');
    const toast = document.createElement('div');
    toast.className = 'admin-toast admin-toast--' + (isError ? 'error' : 'success');
    toast.setAttribute('role', isError ? 'alert' : 'status');
    const icon = document.createElement('i');
    icon.className = 'fa-solid ' + (isError ? 'fa-circle-exclamation' : 'fa-circle-check');
    icon.setAttribute('aria-hidden', 'true');
    const span = document.createElement('span');
    span.className = 'admin-toast__text';
    span.textContent = text;
    const close = document.createElement('button');
    close.type = 'button';
    close.className = 'admin-toast__close';
    close.setAttribute('aria-label', 'Đóng');
    close.textContent = '\u00d7';
    toast.append(icon, span, close);
    host().appendChild(toast);
    arm(toast);
    return toast;
  }

  function flash(kind, message) {
    try { sessionStorage.setItem(FLASH_KEY, JSON.stringify({ kind, message })); } catch { /* private mode */ }
  }

  /** Success: queue the message, then reload (or go to url). */
  function reloadWith(message, url) {
    flash('success', message || 'Đã lưu.');
    if (url) location.href = url;
    else location.reload();
  }

  /** JSON { ok:false, error } (or any failed payload) to an error toast. */
  function jsonError(data, fallback) {
    return show('error', data?.error || data?.message || fallback || DEFAULT_ERROR);
  }

  /**
   * After a modal form re-renders with validation errors: toast the first form-level message
   * (validation summary / [data-form-error]) or a generic hint. Returns true when errors were found.
   */
  function formErrors(container) {
    if (!container) return false;
    const summary = container.querySelector('.validation-summary-errors li, [data-form-error]');
    const summaryText = summary?.textContent?.trim();
    const field = container.querySelector('.field-validation-error, .input-validation-error');
    if (summaryText) { show('error', summaryText); return true; }
    if (field) { show('error', INVALID_FORM); return true; }
    return false;
  }

  window.adminToast = {
    success: (m) => show('success', m),
    error: (m) => show('error', m),
    flash,
    reloadWith,
    jsonError,
    formErrors
  };

  function init() {
    document.querySelectorAll('[data-server-toast]').forEach(arm);
    let pending = null;
    try {
      pending = JSON.parse(sessionStorage.getItem(FLASH_KEY) || 'null');
      sessionStorage.removeItem(FLASH_KEY);
    } catch { pending = null; }
    if (pending?.message) show(pending.kind === 'error' ? 'error' : 'success', pending.message);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();
})();
