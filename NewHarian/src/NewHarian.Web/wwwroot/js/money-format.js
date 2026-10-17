/**
 * VND display helpers + money input focus/blur formatting (1.500.000 VNĐ).
 * Mark inputs with data-money-input or .js-money-input.
 */
(() => {
  const SUFFIX = 'VNĐ';

  function parse(raw) {
    if (raw == null) return null;
    let s = String(raw).trim().replace(/\s*(VNĐ|VND|đ|Đ)\s*$/i, '').trim();
    if (!s) return null;
    if (/^-?\d{1,3}(\.\d{3})+$/.test(s)) s = s.replace(/\./g, '');
    else if (!/^-?\d+$/.test(s)) return null;
    const n = Number(s);
    return Number.isFinite(n) ? n : null;
  }

  function format(amount) {
    const n = typeof amount === 'number' ? amount : parse(amount);
    if (n == null || !Number.isFinite(n)) return '';
    return Math.round(n).toLocaleString('vi-VN') + ' ' + SUFFIX;
  }

  function formatSigned(amount) {
    const n = typeof amount === 'number' ? amount : parse(amount);
    if (n == null || !Number.isFinite(n)) return '';
    return n < 0 ? '-' + format(Math.abs(n)) : format(n);
  }

  function editValue(amount) {
    const n = typeof amount === 'number' ? amount : parse(amount);
    if (n == null || !Number.isFinite(n)) return '';
    return String(Math.round(n));
  }

  function bindInput(input) {
    if (!(input instanceof HTMLInputElement) || input.dataset.moneyBound === '1') return;
    input.dataset.moneyBound = '1';
    input.type = 'text';
    input.inputMode = 'numeric';
    input.autocomplete = 'off';

    const toFormatted = () => {
      const v = parse(input.value);
      if (v == null) {
        if (input.value.trim() === '') return;
        return;
      }
      input.value = format(v);
    };
    const toEdit = () => {
      const v = parse(input.value);
      input.value = v == null ? '' : editValue(v);
    };

    input.addEventListener('focus', toEdit);
    input.addEventListener('blur', toFormatted);
    input.addEventListener('money:refresh', toFormatted);
    if (input.value.trim()) toFormatted();
  }

  function bind(root) {
    (root || document).querySelectorAll('[data-money-input], .js-money-input').forEach(bindInput);
  }

  /** Strip formatting so model binders receive plain integers. */
  function prepareForm(form) {
    if (!form) return;
    form.querySelectorAll('[data-money-input], .js-money-input').forEach(input => {
      const v = parse(input.value);
      input.value = v == null ? (input.value.trim() === '' ? '' : input.value) : editValue(v);
    });
  }

  function refresh(input) {
    if (!input) return;
    bindInput(input);
    input.dispatchEvent(new Event('money:refresh'));
  }

  document.addEventListener('submit', e => {
    if (e.target instanceof HTMLFormElement) prepareForm(e.target);
  }, true);

  document.addEventListener('DOMContentLoaded', () => bind(document));

  window.MoneyFormat = { parse, format, formatSigned, editValue, bind, bindInput, prepareForm, refresh, SUFFIX };
})();
