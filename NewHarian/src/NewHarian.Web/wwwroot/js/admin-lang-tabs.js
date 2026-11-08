// Language tabs for admin forms. Inactive panels stay in the form so their fields still submit.
(() => {
  function panels(root) {
    return [...root.querySelectorAll(':scope > [data-lang-panel]')];
  }

  function tabs(root) {
    return [...root.querySelectorAll(':scope > .lang-tabs__bar [data-lang-tab]')];
  }

  function resizeEditors(panel) {
    if (!panel || !window.tinymce) return;
    panel.querySelectorAll('textarea.richtext').forEach(ta => {
      const editor = tinymce.get(ta.id);
      if (!editor) return;
      const box = editor.getContainer?.();
      if (box) box.style.width = '100%';
      editor.dispatch?.('ResizeEditor');
    });
  }

  function mark(root) {
    tabs(root).forEach(tab => {
      const panel = panels(root).find(item => item.dataset.langPanel === tab.dataset.langTab);
      const missing = !!panel?.querySelector('[required]:not([disabled])')
        && [...panel.querySelectorAll('[required]:not([disabled])')].some(field => !String(field.value || '').trim());
      tab.classList.toggle('is-missing', missing);
    });
  }

  function show(root, lang) {
    tabs(root).forEach(tab => {
      const on = tab.dataset.langTab === lang;
      tab.classList.toggle('is-active', on);
      tab.setAttribute('aria-selected', on ? 'true' : 'false');
      tab.tabIndex = on ? 0 : -1;
    });
    let active = null;
    panels(root).forEach(panel => {
      const on = panel.dataset.langPanel === lang;
      panel.classList.toggle('is-active', on);
      panel.hidden = !on;
      if (on) active = panel;
    });
    root.dispatchEvent(new CustomEvent('admin-lang-shown', { bubbles: true, detail: { lang } }));
    resizeEditors(active);
    mark(root);
  }

  function firstHiddenMissing(root) {
    for (const panel of panels(root)) {
      if (!panel.hidden) continue;
      const field = [...panel.querySelectorAll('[required]:not([disabled])')]
        .find(item => !String(item.value || '').trim());
      if (field) return field;
    }
    return null;
  }

  function revealErrors(root) {
    const bad = root.querySelector('[data-lang-panel] .input-validation-error, [data-lang-panel] .field-validation-error');
    const panel = bad?.closest('[data-lang-panel]');
    if (panel?.dataset.langPanel) show(root, panel.dataset.langPanel);
    else mark(root);
  }

  document.addEventListener('click', event => {
    const tab = event.target.closest?.('[data-lang-tab]');
    if (!tab) return;
    const root = tab.closest('[data-lang-tabs]');
    if (!root) return;
    show(root, tab.dataset.langTab);
  });

  document.addEventListener('keydown', event => {
    const tab = event.target.closest?.('[data-lang-tab]');
    if (!tab || (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft')) return;
    const list = tabs(tab.closest('[data-lang-tabs]'));
    const index = list.indexOf(tab);
    if (index < 0) return;
    const next = event.key === 'ArrowRight'
      ? list[(index + 1) % list.length]
      : list[(index - 1 + list.length) % list.length];
    next.focus();
    show(tab.closest('[data-lang-tabs]'), next.dataset.langTab);
    event.preventDefault();
  });

  document.addEventListener('input', event => {
    const root = event.target.closest?.('[data-lang-tabs]');
    if (root) mark(root);
  });

  document.addEventListener('submit', event => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement)) return;
    for (const root of form.querySelectorAll('[data-lang-tabs]')) {
      const field = firstHiddenMissing(root);
      if (!field) continue;
      const panel = field.closest('[data-lang-panel]');
      event.preventDefault();
      event.stopImmediatePropagation();
      show(root, panel.dataset.langPanel);
      field.focus();
      field.reportValidity?.();
      return;
    }
  }, true);

  function scan(node) {
    if (!(node instanceof Element)) return;
    const roots = node.matches('[data-lang-tabs]') ? [node] : [...node.querySelectorAll('[data-lang-tabs]')];
    roots.forEach(revealErrors);
  }

  document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('[data-lang-tabs]').forEach(revealErrors);
  });

  new MutationObserver(records => {
    for (const record of records) {
      record.addedNodes.forEach(scan);
    }
  }).observe(document.documentElement, { childList: true, subtree: true });
})();
